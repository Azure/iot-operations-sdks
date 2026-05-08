# DESCRIPTION: Generates a Markdown-formatted coverage summary table
# from a JSON-formatted LLVM coverage report.
#
# PARAMETERS:
# - Standard input: JSON-formatted LLVM coverage report.
# - Named:
#   - $bad: Integer threshold below which coverage percentage is "bad".
#   - $good: Integer threshold above which coverage percentage is
#     "good".
#   - $manifest: Path to a `Cargo.toml`.  Should be absolute and a
#     workspace manifest for ideal path relativization.
#   - $root: Absolute path of the repository root.
#   - $short_name: Base name of the directory containing the crate
#     manifest.

# NOTE: `.data` is a JSON array, but LLVM's coverage exporter currently
# only ever emits a single element. The `.data[]` iteration below
# relies on that assumption; revisit if a toolchain upgrade changes it.
# Ref: https://github.com/llvm/llvm-project/blob/main/llvm/tools/llvm-cov/CoverageExporterJson.cpp

def symbol:
  if . < $bad then "\u274c"
  elif . < $good then "\u2796"
  else "\u2795"
  end;

  ($manifest | rtrimstr("Cargo.toml")) as $manifest_directory
# NOTE: Extra newline is necessary after `</summary>` since otherwise
# the table does not get formatted.
| [ "<details><summary>\($short_name): \($manifest | ltrimstr($root + "/"))</summary>\n"
  , "Crate | Coverage | Status"
  , "----- | ----- | -----"
  , ( .data[]
    | ( ( [ .files
          # NOTE: This intentionally allows the crate described by a
          # workspace manifest to be identified by a blank string: the
          # directory name is already present in `$short_name`.
          | map_values(.crate = (.filename | split("src/") | first))
          | group_by(.crate)[]
          | { crate: (first | .crate | ltrimstr($manifest_directory))
            , coverage:
                ( reduce .[].summary.lines as $item
                    ( { count: 0, covered: 0 }
                    ; with_entries(.value += $item[.key])
                    )
                | .covered / .count * 100
                | floor
                )
            }
          ]
        # NOTE: Sorting is usually unnecessary, but is required when
        # the directory prefix is trimmed from a subset of crate paths.
        # The most obvious example is when the manifest is for a crate,
        # wherein the crate's coverage summary is shifted to the first
        # row.
        | sort_by(.crate)[]
        | "\(.crate) | \(.coverage)% | \(.coverage | symbol)"
        )
      , ( .totals.lines
        | (.percent | floor) as $percent
        | "**Summary** | **\($percent)%**  (\(.covered) / \(.count)) | \($percent | symbol)"
        )
      )
    )
  , "</details>"
  ]
| join("\n")
