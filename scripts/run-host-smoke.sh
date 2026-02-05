#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 4 ]]; then
  echo "Usage: $0 <project_path> <tfm> <port> <bucket_name>" >&2
  exit 1
fi

project_path="$1"
tfm="$2"
port="$3"
bucket_name="$4"
project_dir="$(dirname "$project_path")"
local_settings="$project_dir/appsettings.Local.json"
log_file="$project_dir/smoke-${tfm}.log"
pid_file="$project_dir/smoke-${tfm}.pid"

cleanup() {
  if [[ -f "$pid_file" ]]; then
    pid="$(cat "$pid_file")"
    if kill -0 "$pid" 2>/dev/null; then
      kill "$pid" || true
      wait "$pid" 2>/dev/null || true
    fi
    rm -f "$pid_file"
  fi
}

trap cleanup EXIT

cat > "$local_settings" <<JSON
{
  "ConnectionStrings": {
    "umbracoDbDSN": "Data Source=|DataDirectory|/Smoke.${tfm}.sqlite.db;Cache=Shared;Foreign Keys=True;Pooling=True"
  },
  "AWS": {
    "Region": "us-east-1",
    "ServiceURL": "http://127.0.0.1:9000",
    "ForcePathStyle": true,
    "AuthenticationRegion": "us-east-1",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin"
  },
  "Umbraco": {
    "CMS": {
      "Unattended": {
        "InstallUnattended": true,
        "UpgradeUnattended": true,
        "UnattendedUserName": "smoke-admin",
        "UnattendedUserEmail": "smoke@example.local",
        "UnattendedUserPassword": "SmokePassword123!"
      }
    },
    "Storage": {
      "AWSS3": {
        "Media": {
          "BucketName": "${bucket_name}",
          "Region": "us-east-1"
        }
      }
    }
  }
}
JSON

AF_SMOKE_TESTS=1 \
AWS_ACCESS_KEY_ID=minioadmin \
AWS_SECRET_ACCESS_KEY=minioadmin \
AWS_REGION=us-east-1 \
ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project "$project_path" -c Release -f "$tfm" --no-build --urls "http://127.0.0.1:${port}" > "$log_file" 2>&1 &

echo $! > "$pid_file"

echo "Waiting for host on port ${port}..."
for _ in $(seq 1 180); do
  if curl -fsS "http://127.0.0.1:${port}/smoke/health" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl -fsS "http://127.0.0.1:${port}/smoke/health" >/dev/null 2>&1; then
  echo "Host failed to boot for ${project_path} (${tfm})." >&2
  cat "$log_file" >&2 || true
  exit 1
fi

response="$(curl -fsS -X POST "http://127.0.0.1:${port}/smoke/media-upload")"
echo "$response"

if [[ "$response" != *'"exists":true'* ]]; then
  echo "Media upload smoke test failed: unexpected response." >&2
  cat "$log_file" >&2 || true
  exit 1
fi

echo "Smoke test passed for ${project_path} (${tfm})."
