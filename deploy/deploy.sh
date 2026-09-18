#!/usr/bin/env bash
set -Eeuo pipefail

# Dit script draait op de VPS vanuit een checkout van deze repository.
solution_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_id="$(date -u +%Y%m%d%H%M%S)"
release_root="/opt/balkishassan/releases"
release_path="${release_root}/${release_id}"
persistent_uploads="/var/lib/balkishassan/uploads"
publish_path="${solution_root}/artifacts/publish"

dotnet restore "${solution_root}/BalkisHassan.sln"
dotnet test "${solution_root}/BalkisHassan.sln" --configuration Release --no-restore
dotnet publish "${solution_root}/src/BalkisHassan.Web/BalkisHassan.Web.csproj" \
  --configuration Release --no-restore --output "${publish_path}"

sudo install -d -o balkishassan -g balkishassan "${release_root}" "${persistent_uploads}"
sudo install -d -o balkishassan -g balkishassan "${release_path}"
sudo rsync -a "${publish_path}/" "${release_path}/"

# De eerste release vult de persistente mediaplaats met de gemigreerde bestanden.
if [[ -d "${release_path}/wwwroot/uploads" ]]; then
  sudo rsync -a --ignore-existing "${release_path}/wwwroot/uploads/" "${persistent_uploads}/"
  sudo find "${release_path}/wwwroot/uploads" -depth -delete
fi
sudo ln -s "${persistent_uploads}" "${release_path}/wwwroot/uploads"
sudo chown -R balkishassan:balkishassan "${release_path}" "${persistent_uploads}"
sudo ln -sfn "${release_path}" /opt/balkishassan/current
sudo systemctl restart balkishassan
sudo systemctl --no-pager --full status balkishassan
