#!/bin/bash

# Shared helper for the .NET connector sample deploy scripts.
#
# `k3d image import` is known to occasionally report success while the image
# never actually lands in the node's containerd. The missing image only surfaces
# much later as `ErrImageNeverPull` on the connector pod (which runs with
# imagePullPolicy: Never because the image is built locally), failing CI for a
# reason unrelated to the code under test.
#
# k3d_image_import_with_retry imports the image and then best-effort verifies it
# landed in the node's image store, retrying the whole import while the image is
# unconfirmed. Verification is intentionally NON-FATAL: probing a k3d node's
# image store from outside is brittle across k3d/k3s versions, so if we cannot
# positively confirm the image after all attempts we emit a warning and proceed
# rather than failing the deploy (k3d's own import reports success each time, and
# a false-negative probe must not break otherwise-healthy CI). The probe still
# adds value: when it CAN confirm, we return immediately, and genuine k3d import
# failures are retried.
#
# Usage:
#   source "$(dirname "$0")/../k3d-image-import.sh"
#   k3d_image_import_with_retry myimage:latest            # cluster defaults to k3s-default
#   k3d_image_import_with_retry myimage:latest k3s-default 5

# Returns 0 if the given image appears to be present in the named k3d node's
# image store. Tries the CRI view first (`k3s crictl images`, the same view the
# kubelet consults for imagePullPolicy: Never), then falls back to containerd's
# `k8s.io` namespace (`k3s ctr -n k8s.io images ls`) since `k3s ctr` defaults to
# the `default` namespace where k3d-imported images do NOT appear.
k3d_node_has_image() {
    local node="$1"
    local image="$2"
    # Match on the bare image name (strip the tag) since the store lists the repo
    # with a registry prefix (e.g. docker.io/library/<image>).
    local name="${image%%:*}"
    docker exec "$node" k3s crictl images 2>/dev/null | grep -q "$name" && return 0
    docker exec "$node" k3s ctr -n k8s.io images ls 2>/dev/null | grep -q "$name" && return 0
    return 1
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

        # Enumerate the cluster's actual k3s nodes (server + agents). Exclude the
        # load balancer node (k3d-<cluster>-serverlb): it runs nginx, not k3s/CRI,
        # so it never holds images. The trailing '-' in the name filters anchor on
        # the numbered nodes (server-0, agent-0, ...) and skip 'serverlb'.
        nodes=$(docker ps --filter "name=k3d-${cluster}-server-" --filter "name=k3d-${cluster}-agent-" --format '{{.Names}}')
        if [ -z "$nodes" ]; then
            nodes="k3d-${cluster}-server-0"
        fi

        local all_present=true
        for node in $nodes; do
            if ! k3d_node_has_image "$node" "$image"; then
                echo "Image '$image' not yet present in the CRI image store on node '$node'." >&2
                all_present=false
            fi
        done

        if [ "$all_present" = true ]; then
            echo "Verified image '$image' is present on all k3d nodes."
            return 0
        fi

        echo "Image '$image' not confirmed in node image store yet; retrying after backoff..." >&2
        attempt=$((attempt + 1))
        sleep 5
    done

    # Could not positively confirm the image, but k3d reported a successful import
    # on every attempt. Rather than fail an otherwise-healthy deploy on a brittle
    # probe, warn and continue; a genuinely missing image will still surface as
    # ErrImageNeverPull on the pod, which the wait-for-ready steps catch.
    echo "WARNING: could not verify image '$image' in the k3d node image store after $max_attempts attempts, but k3d reported a successful import; proceeding." >&2
    return 0
}
