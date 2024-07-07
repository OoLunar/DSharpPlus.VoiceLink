#!/bin/bash

# Check if the output file exists and if we've already built it for this version
EXPORT_DIR="$WORKSPACE/libs/libopus/$RID/native"
if [ -f "$EXPORT_DIR/$FILE" ]; then
  COMMIT_MESSAGE="$(git log -1 --pretty=%B -- "$EXPORT_DIR/$FILE")"
  if [[ "$COMMIT_MESSAGE" == *"$OPUS_VERSION"* ]]; then
    echo "Already built $FILE for $OPUS_VERSION"
    exit 0
  fi
fi

# Create Opus dir
mkdir -p "$WORKSPACE/libs/opus"
cd "$WORKSPACE/libs/opus"

# Clone the repository
git clone https://gitlab.xiph.org/xiph/opus .
git fetch --tags

# Export the latest tag
OPUS_VERSION="$(git describe --tags $(git rev-list --tags --max-count=1))"
echo "version=$(echo $OPUS_VERSION | perl -pe '($_)=/([0-9]+([.][0-9]+)+)/')" >> $GITHUB_OUTPUT

# Checkout the latest tag
git checkout "$OPUS_VERSION"

# Build the library
cmake -S . -B build $COMMAND_ARGS -DOPUS_BUILD_SHARED_LIBRARY=ON -DCMAKE_BUILD_TYPE=Release -Wno-dev
cmake --build build --config Release

# Move the output file to the correct location
mkdir -p              "$EXPORT_DIR"
rm -f                 "$EXPORT_DIR/$FILE"
mv build/$FIND_FILE   "$EXPORT_DIR/$FILE"

# Delay committing to prevent race conditions
sleep "$(( $JOB_INDEX * 5 ))"