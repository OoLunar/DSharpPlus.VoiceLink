#!/bin/bash

# Deps
xbps-install -Syu > /dev/null
xbps-install -y ImageMagick yarn > /dev/null
yarn global add svgo > /dev/null

# Functions
regenerate()
{
  echo "Generating assets for $1"

  # Optimize the SVG file
  svgo --multipass --quiet "$1"

  # Convert to PNG
  convert "$1" -size 1024x1024 "${1%.*}.png"

  # Convert to ICO
  # https://stackoverflow.com/a/15104985
  convert "$1" -bordercolor white -border 0 \
    \( -clone 0 -resize 16x16 \) \
    \( -clone 0 -resize 32x32 \) \
    \( -clone 0 -resize 48x48 \) \
    \( -clone 0 -resize 64x64 \) \
    -delete 0 -alpha off -colors 256 "${1%.*}.ico"
}

# Iterate over each file matching the pattern "*.svg" in the "res" directory
for file in res/*.svg; do
    # Execute the "regenerate" command on each file
    regenerate "$file"
done

# Check if any files were modified
git config --global user.email "github-actions[bot]@users.noreply.github.com"
git config --global user.name "github-actions[bot]"
git add res > /dev/null
git diff-index --quiet HEAD
if [ "$?" == "1" ]; then
  git commit -m "[ci-skip] Regenerate resource files." > /dev/null
  git push > /dev/null
else
  echo "No resource files were modified."
fi