#!/bin/bash

# Check if the output file exists and if we've already built it for this version
EXPORT_DIR="$WORKSPACE/libs/libsodium/$RID/native"
if [ -f "$EXPORT_DIR/$FILE" ]; then
  COMMIT_MESSAGE="$(git log -1 --pretty=%B -- "$EXPORT_DIR/$FILE")"
  if [[ "$COMMIT_MESSAGE" == *"$SODIUM_VERSION"* ]]; then
    echo "Already built $FILE for $SODIUM_VERSION"
    exit 0
  fi
fi

# Create Sodium dir
mkdir -p "$WORKSPACE/libs/sodium"
cd "$WORKSPACE/libs/sodium"

# Clone the repository
git clone https://github.com/jedisct1/libsodium.git .
git fetch --tags

# Export the latest tag
SODIUM_VERSION="$(git describe --tags $(git rev-list --tags --max-count=1))"
echo "version=$(echo $SODIUM_VERSION | perl -pe '($_)=/([0-9]+([.][0-9]+)+)/')" >> $GITHUB_OUTPUT

# Checkout the latest tag
git checkout "$SODIUM_VERSION"

# Automatically exit if the build fails
set -e

# Build the library
$COMMAND

# Move the output file to the correct location
mkdir -p          "$EXPORT_DIR"
rm -f             "$EXPORT_DIR/$FILE"
mv "$OUTPUT_FILE" "$EXPORT_DIR/$FILE"

# Delay committing to prevent race conditions
sleep "$(( $JOB_INDEX * 5 ))"