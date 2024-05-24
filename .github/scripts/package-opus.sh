#!/bin/bash

# Create Opus dir
mkdir -p "$WORKSPACE/libs/opus"
cd "$WORKSPACE/libs/opus"

# Clone the repository
git clone https://gitlab.xiph.org/xiph/opus .
git fetch --tags

# Export the latest tag
OPUS_VERSION="$(git describe --tags $(git rev-list --tags --max-count=1))"
echo "OPUS_VERSION=$OPUS_VERSION" >> $GITHUB_ENV

# Checkout the latest tag
git checkout "$OPUS_VERSION"

# Build the library
cmake -S . -B build $COMMAND_ARGS -DOPUS_BUILD_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release -Wno-dev
cmake --build build --config Release

EXPORT_DIR="$WORKSPACE/libs/libopus/$RID/native/"

# Do NOT exit on error
set +e

# Check if the output file has changed, since sometimes Git struggles on Windows
cmp --silent build/$FIND_FILE "$EXPORT_DIR/$FILE"
if [ $? -eq 0 ]; then
  echo 'No changes detected'
  exit 0
fi

# Exit on error
set -e

# Move the output file to the correct location
mkdir -p              "$EXPORT_DIR"
rm -f                 "$EXPORT_DIR/$FILE"
mv build/$FIND_FILE   "$EXPORT_DIR/$FILE"

# Delay committing to prevent race conditions
sleep "$(( $JOB_INDEX * 2 ))"