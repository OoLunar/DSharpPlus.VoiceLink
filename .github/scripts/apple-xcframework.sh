#!/bin/sh

# Set up environment variables
export PREFIX="$(pwd)/libsodium-apple"
export IOS_VERSION_MIN="${IOS_VERSION_MIN-"9.0.0"}"

# Determine number of processors
PROCESSORS=$(getconf NPROCESSORS_ONLN 2>/dev/null || getconf _NPROCESSORS_ONLN 2>/dev/null)
PROCESSORS=${PROCESSORS:-3}

# Function to generate Swift module map
swift_module_map() {
  cat <<EOF
module Clibsodium {
    header "sodium.h"
    export *
}
EOF
}

# Function to build for iOS
build_ios() {
  SDK="$(xcode-select -p)/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk"

  # Set compiler and linker flags for 64-bit iOS
  CFLAGS="-Ofast -arch arm64 -isysroot ${SDK} -mios-version-min=${IOS_VERSION_MIN}"
  LDFLAGS="-arch arm64 -isysroot ${SDK} -mios-version-min=${IOS_VERSION_MIN}"

  # Configure and build
  ./configure --host=arm-apple-darwin10 --prefix="${PREFIX}/tmp/ios64" --enable-minimal || exit 1
  make -j"${PROCESSORS}" install || exit 1
}

# Create necessary directories
mkdir -p "${PREFIX}/tmp"

# Build for iOS
echo "Building for iOS..."
build_ios

# Add Swift module map
echo "Adding the Clibsodium module map for Swift..."
find "$PREFIX" -name "include" -type d -print | while read -r f; do
  swift_module_map >"${f}/module.modulemap"
done

# Bundle iOS targets
echo "Bundling iOS targets..."
mkdir -p "${PREFIX}/ios/lib"
cp -a "${PREFIX}/tmp/ios64/include" "${PREFIX}/ios/"
lipo -create "${PREFIX}/tmp/ios64/lib/libsodium.dylib" -output "${PREFIX}/ios/lib/libsodium.dylib"
echo "Done!"