#!/bin/bash

# Create Sodium dir
mkdir -p "$WORKSPACE/libs/sodium"
cd "$WORKSPACE/libs/sodium"

# Clone the repository
git clone https://github.com/jedisct1/libsodium.git .
git fetch --tags

# Export the latest tag
SODIUM_VERSION="$(git describe --tags $(git rev-list --tags --max-count=1))"
echo "SODIUM_VERSION=$SODIUM_VERSION" >> $GITHUB_ENV

# Checkout the latest tag
git checkout "$SODIUM_VERSION"

# Build the library
$COMMAND
EXPORT_DIR="$WORKSPACE/libs/libsodium/$RID/native/"

# Do NOT exit on error
set +e

# Check if the output file has changed, since sometimes Git struggles on Windows
cmp --silent "$OUTPUT_FILE" "$EXPORT_DIR/$FILE"
if [ $? -eq 0 ]; then
  echo 'No changes detected'
  exit 0
fi

# Exit on error
set -e

# Move the output file to the correct location
mkdir -p          "$EXPORT_DIR"
rm -f             "$EXPORT_DIR/$FILE"
mv "$OUTPUT_FILE" "$EXPORT_DIR/$FILE"

# Delay committing to prevent race conditions
sleep "$(( $JOB_INDEX * 2 ))"