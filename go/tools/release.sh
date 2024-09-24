#!/bin/sh
REPO=github.com/Azure/iot-operations-sdks
ROOT=$(git rev-parse --show-toplevel)

# This script automates GitHub releases for the Go SDKs based on the versions
# present in the go.work file. It uses the existing Git tags to determine
# whether a new release should occur (and to determine the previous version),
# so re-running it on existing releases is a no-op. Calling it with "dry-run"
# prints pending release notes without making any changes.
if [ "$1" = "dry-run" ]; then
    release() { echo "$@" && echo && cat ; }
else
    release() { gh release create "$@" -F - ; }
fi

# Find replace directives in the go.work file matching the SDKs and extract the
# module path and version. Since these versions must be updated to the published
# releases to facilitate development, we can use them as the source of truth.
sed -nr "s_^\t$REPO/(.*) v(.*) => .*\$_\1 \2_p" "$ROOT/go/go.work" |\
while read MOD VER ; do
    # Attempt to apply the Git tag locally. This will fail if the tag is already
    # present (e.g. this version has already been released), skipping this SDK.
    TAG="$MOD/v$VER"
    if git tag "$TAG" 2> /dev/null ; then
        # Use Git version sorting to find the previous version of this SDK
        # relative to the new tag.
        PREV=$(git -c 'versionsort.suffix=-' tag -l --sort=v:refname "$MOD/*" |\
            grep -xB1 "$TAG" | head -1)

        # If we didn't find a previous version, include all changes in the
        # release notes; otherwise, only include the change delta.
        RANGE=$([ "$PREV" = "$TAG" ] && echo "HEAD" || echo "$PREV..HEAD")

        # Mark versions with prerelease markers as prerelease.
        PRE=$(echo "$VER" | grep -q - && echo "--prerelease --latest=false")

        # Remove the local tag to make sure it doesn't interfere with anything.
        git tag -d "$TAG" > /dev/null

        # Generate the release notes and perform the release.
        release "$TAG" $PRE --title "[Go] ${MOD##*/}@v$VER" << EOF
A new release is available for $MOD. To install, run:

\`\`\`
go get $REPO/$MOD@v$VER
\`\`\`

### Changelog

$(git log --pretty=oneline --abbrev-commit --no-decorate --no-color \
    --no-merges --reverse --format="- [%h](https://$REPO/commit/%H) %s" \
    "$RANGE" -- "$ROOT/$MOD")
EOF
    else
        echo "nothing to do for $TAG" >&2
    fi
done
