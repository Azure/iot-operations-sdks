#!/bin/bash

# Shared helper for the .NET connector sample deploy scripts.
#
# `k3d image import` is known to occasionally report success while the image
# never actually lands in the node's containerd. The missing image only surfaces
# much later as `ErrImageNeverPull` on the connector pod (which runs with
# imagePullPolicy: Never because the image is built locally), failing CI for a
# reason unrelated to the code under test.
#
# k3d_image_import_with_retry imports the image and then verifies it is present
# in the containerd image store of every k3d node, retrying the whole import on
# failure. It returns non-zero (so `set -e` callers fail fast) if the image is
# still missing after the configured number of attempts.
#
# Usage:
#   source "$(dirname "$0")/../k3d-image-import.sh"
#   k3d_image_import_with_retry myimage:latest            # cluster defaults to k3s-default
#   k3d_image_import_with_retry myimage:latest k3s-default 5

# Returns 0 if the given image is present in the named k3d node's containerd.
# Uses `k3s ctr`, which targets k3s's containerd socket automatically.
k3d_node_has_image() {
    local node="$1"
    local image="$2"
    # Match on the bare image name (strip the tag) since containerd lists it with
    # a registry/repo prefix (e.g. docker.io/library/<image>:latest).
    local name="${image%%:*}"
    docker exec "$node" k3s ctr images ls 2>/dev/null | grep -q "$name"
}

k3d_image_import_with_retry() {
    local image="$1"
    local cluster="${2:-k3s-default}"
    local max_attempts="${3:-5}"
    local attempt=1
    local nodes

    while [ "$attempt" -le "$max_attempts" ]; do
        echo "Importing image '$image' into k3d cluster '$cluster' (attempt $attempt/$max_attempts)..."
        # Don't let a non-zero exit here abort under `set -e`; we verify below and
        # retry deliberately.
        k3d image import "$image" -c "$cluster" || true

        # Enumerate every k3d node container for this cluster (server + agents) so
        # we verify the image regardless of which node the pod is scheduled on.
        nodes=$(docker ps --filter "name=k3d-${cluster}-server" --filter "name=k3d-${cluster}-agent" --format '{{.Names}}')
        if [ -z "$nodes" ]; then
            nodes="k3d-${cluster}-server-0"
        fi

        local all_present=true
        for node in $nodes; do
            if ! k3d_node_has_image "$node" "$image"; then
                echo "Image '$image' not yet present in containerd on node '$node'." >&2
                all_present=false
            fi
        done

        if [ "$all_present" = true ]; then
            echo "Verified image '$image' is present on all k3d nodes."
            return 0
        fi

        echo "Image '$image' missing on at least one node after import; retrying after backoff..." >&2
        attempt=$((attempt + 1))
        sleep 5
    done

    echo "ERROR: failed to import image '$image' into k3d cluster '$cluster' after $max_attempts attempts (still missing on at least one node)." >&2
    return 1
}
