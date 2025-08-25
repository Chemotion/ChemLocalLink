#!/usr/bin/env bash
set -Eeuo pipefail

CLEAN=0
if [[ "${1:-}" == "--clean" || "${1:-}" == "-c" ]]; then
  CLEAN=1
  shift
fi

APP_NAME="${APP_NAME:-ChemLocalLink}"
PKG_NAME="$(printf '%s' "${APP_NAME}" | tr '[:upper:]' '[:lower:]')"
CSPROJ="${CSPROJ:-../ChemLocalLink.csproj}"
CONFIG="${CONFIG:-Release}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
OUT_BASE="${OUT_BASE:-publish}"
PKG_ROOT_BASE="${PKG_ROOT:-pkgroot}"
DEB_BUILD="${DEB_BUILD:-build}"
MAINTAINER="${MAINTAINER:-SCC KIT <mekky@kit.edu>}"
DESCRIPTION="${DESCRIPTION:-ChemLocalLink App}"
ICON_FILE="${ICON_FILE:-../Assets/icon.png}"
SECTION="${SECTION:-science}"
SCHEME_HANDLER="${SCHEME_HANDLER:-chemotion}"
DESKTOP_MIMES="x-scheme-handler/${SCHEME_HANDLER};"
ICON_SIZES=("128x128")
TARGET_ARCHS="${TARGET_ARCHS:-amd64 arm64}"

need() { command -v "$1" >/dev/null 2>&1 || { echo "Error: '$1' is required but not found." >&2; exit 2; }; }
for bin in dpkg dpkg-deb du dotnet grep awk install; do need "$bin"; done
trap 'echo "Build failed (line $LINENO)"; exit 99' ERR

rid_for_arch() {
  case "$1" in
    amd64)  echo "linux-x64" ;;
    arm64)  echo "linux-arm64" ;;
    *)      echo ""; return 1 ;;
  esac
}

# version
[[ -f "${CSPROJ}" ]] || { echo "Error: csproj not found at '${CSPROJ}'" >&2; exit 2; }
RAW_VERSION="$(grep -oPm1 '(?<=<Version>)[^<]+' "${CSPROJ}" || true)"
[[ -n "${RAW_VERSION}" ]] || RAW_VERSION="0.0.0"
VERSION_UP="${RAW_VERSION%%+*}"
VERSION_DEB="${VERSION_UP//-/'~'}"
[[ -n "${VERSION_DEB}" ]] || VERSION_DEB="0.0.0"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "${DEB_BUILD}"

build_one() {
  local DEB_ARCH="$1"
  local RUNTIME; RUNTIME="$(rid_for_arch "${DEB_ARCH}")" || { echo "Unsupported arch '${DEB_ARCH}'"; return 3; }

  local OUT_DIR="${OUT_BASE}/${RUNTIME}"
  local PKG_ROOT="${PKG_ROOT_BASE}-${DEB_ARCH}"
  local DEB_OUT="${DEB_BUILD}/${APP_NAME}_${VERSION_DEB}_${DEB_ARCH}.deb"

  echo "==> Building ${APP_NAME} v${VERSION_DEB} for ${DEB_ARCH} (${RUNTIME})"

  # clean staging
  rm -rf "${OUT_DIR}" "${PKG_ROOT}"
  mkdir -p "${OUT_DIR}" "${PKG_ROOT}"

  # publishing
  dotnet publish "${CSPROJ}" \
    -c "${CONFIG}" \
    -r "${RUNTIME}" \
    -f "${FRAMEWORK}" \
    -o "${OUT_DIR}" \
    --self-contained true \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true \
    /p:IncludeAllContentForSelfExtract=true \
    /p:DebugType=embedded \
    /p:DebugSymbols=false \
    /p:StripSymbols=true

  if [[ ! -f "${OUT_DIR}/${APP_NAME}" ]]; then
    echo "Error: Executable '${OUT_DIR}/${APP_NAME}' not found." >&2
    ls -l "${OUT_DIR}" >&2
    return 4
  fi
  chmod 0755 "${OUT_DIR}/${APP_NAME}"

  # layout
  install -d \
    "${PKG_ROOT}/DEBIAN" \
    "${PKG_ROOT}/usr/lib/${APP_NAME}" \
    "${PKG_ROOT}/usr/share/applications" \
    "${PKG_ROOT}/usr/bin"

  # icon dir
  for size in "${ICON_SIZES[@]}"; do
    install -d "${PKG_ROOT}/usr/share/icons/hicolor/${size}/apps"
  done

  # app payload
  cp -a "${OUT_DIR}/." "${PKG_ROOT}/usr/lib/${APP_NAME}/"

  # desktop entry
  cat > "${PKG_ROOT}/usr/share/applications/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=${APP_NAME}
Comment=${DESCRIPTION}
Exec=/usr/bin/${APP_NAME} %U
TryExec=/usr/bin/${APP_NAME}
Icon=${APP_NAME}
Terminal=false
Type=Application
Categories=Science;Utility;
MimeType=${DESKTOP_MIMES}
EOF
  chmod 0644 "${PKG_ROOT}/usr/share/applications/${APP_NAME}.desktop"

  # icon
  if [[ -f "${ICON_FILE}" ]]; then
    for size in "${ICON_SIZES[@]}"; do
      install -m 0644 "${ICON_FILE}" "${PKG_ROOT}/usr/share/icons/hicolor/${size}/apps/${APP_NAME}.png"
    done
  else
    echo "Warning: icon '${ICON_FILE}' missing." >&2
  fi

  # PATH
  cat > "${PKG_ROOT}/usr/bin/${APP_NAME}" <<EOF
#!/usr/bin/env bash
set -euo pipefail
exec /usr/lib/${APP_NAME}/${APP_NAME} "\$@"
EOF
  chmod 0755 "${PKG_ROOT}/usr/bin/${APP_NAME}"

  # maintainer scripts
  cat > "${PKG_ROOT}/DEBIAN/postinst" <<EOF
#!/usr/bin/env bash
set -e
MIMES="$(printf '%s' '${DESKTOP_MIMES}' | sed 's/;\\{1,\\}\$//' | tr ';' ' ')"

command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
fi

if command -v xdg-mime >/dev/null 2>&1; then
  for mt in \$MIMES; do xdg-mime default ${APP_NAME}.desktop "\$mt" || true; done
fi
if command -v gio >/dev/null 2>&1; then
  for mt in \$MIMES; do gio mime "\$mt" ${APP_NAME}.desktop || true; done
fi

command -v xdg-desktop-menu >/dev/null 2>&1 && xdg-desktop-menu forceupdate || true
exit 0
EOF
  chmod 0755 "${PKG_ROOT}/DEBIAN/postinst"

  cat > "${PKG_ROOT}/DEBIAN/postrm" <<'EOF'
#!/usr/bin/env bash
set -e
case "$1" in
  remove|purge)
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then
      gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor || true
    fi
    command -v xdg-desktop-menu >/dev/null 2>&1 && xdg-desktop-menu forceupdate || true
    ;;
esac
exit 0
EOF
  chmod 0755 "${PKG_ROOT}/DEBIAN/postrm"

  # control file
  local INSTALLED_SIZE
  INSTALLED_SIZE="$(du -ks "${PKG_ROOT}/usr" | awk '{print $1}')"

  cat > "${PKG_ROOT}/DEBIAN/control" <<EOF
Package: ${PKG_NAME}
Version: ${VERSION_DEB}
Section: ${SECTION}
Priority: optional
Architecture: ${DEB_ARCH}
Maintainer: ${MAINTAINER}
Installed-Size: ${INSTALLED_SIZE}
Depends: libc6, libgcc-s1 | libgcc1, libstdc++6, libglib2.0-0, libgtk-3-0 | libx11-6, libxcb1, libxrandr2, libxrender1, libxi6, libxtst6, libnss3, libatk1.0-0, libdrm2, libgbm1, libgl1, libegl1, libfontconfig1, libfreetype6, libharfbuzz0b, desktop-file-utils, xdg-utils, shared-mime-info, hicolor-icon-theme
Recommends: libicu76 | libicu75 | libicu74 | libicu73 | libicu72 | libicu71 | libicu70 | libicu69 | libicu67 | libicu66
Description: ${DESCRIPTION}
 Multi-platform Avalonia application with a custom URL scheme handler (${SCHEME_HANDLER}://).
EOF
  chmod 0644 "${PKG_ROOT}/DEBIAN/control"

  find "${PKG_ROOT}" -type d -exec chmod 0755 {} +
  chmod 0755 "${PKG_ROOT}/usr/lib/${APP_NAME}/${APP_NAME}"
  chmod 0755 "${PKG_ROOT}/usr/bin/${APP_NAME}"

  # build .deb
  if command -v fakeroot >/dev/null 2>&1; then
    fakeroot dpkg-deb -Zgzip -z9 --build "${PKG_ROOT}" "${DEB_OUT}"
  else
    if dpkg-deb --help 2>&1 | grep -q -- '--root-owner-group'; then
      dpkg-deb --root-owner-group -Zgzip -z9 --build "${PKG_ROOT}" "${DEB_OUT}"
    else
      echo "Warning: fakeroot missing and dpkg-deb lacks --root-owner-group" >&2
      dpkg-deb -Zgzip -z9 --build "${PKG_ROOT}" "${DEB_OUT}"
    fi
  fi

  echo "Built ${DEB_OUT}"
}

echo "Targets: ${TARGET_ARCHS}"
for ARCH in ${TARGET_ARCHS}; do
  build_one "${ARCH}"
done

if (( CLEAN )); then
  echo "Cleaning staging and publish artifacts…"
  for ARCH in ${TARGET_ARCHS}; do
    rm -rf "${PKG_ROOT_BASE}-${ARCH}"
  done
  rm -rf "${OUT_BASE}"
  echo "Clean done. Kept only: ${DEB_BUILD}/"
fi

echo "All done. Packages in: ${DEB_BUILD}/"
