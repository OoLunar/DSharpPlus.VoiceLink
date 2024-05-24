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

# Check if the output file exists and if we've already built it for this version
EXPORT_DIR="$WORKSPACE/libs/libsodium/$RID/native/"
if [ -f "$EXPORT_DIR/$FILE" ]; then
  COMMIT_MESSAGE="$(git log -1 --pretty=%B -- "$EXPORT_DIR/$FILE")"
  if [[ "$COMMIT_MESSAGE" == *"$SODIUM_VERSION"* ]]; then
    echo "Already built $FILE for $SODIUM_VERSION"
    exit 0
  fi
fi

# Checkout the latest tag
git checkout "$SODIUM_VERSION"

# Build the library
$COMMAND

# Move the output file to the correct location
mkdir -p          "$EXPORT_DIR"
rm -f             "$EXPORT_DIR/$FILE"
mv "$OUTPUT_FILE" "$EXPORT_DIR/$FILE"

# Delay committing to prevent race conditions
sleep "$(( $JOB_INDEX * 5 ))"