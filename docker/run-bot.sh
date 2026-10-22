#!/usr/bin/env bash
# Both entry points use the existing shared lock; only logs live in per-run directories.
set -u
umask 077
kind=${1:?Expected pr or merge}
shift
case "$kind" in pr|merge) ;; *) echo 'Expected pr or merge.' >&2; exit 2 ;; esac
(( $# > 0 )) || exit 2
workspace=/root/.local/share/NugetNinjaWorkspace
logs="$workspace/logs"
retention_seconds=$((14 * 24 * 60 * 60))
budget_bytes=$((256 * 1024 * 1024))
mkdir -p "$logs" || exit 1
chmod 700 "$logs" || exit 1

# Only inactive directories are eligible. The per-run lock is inherited by writers,
# so killing the wrapper alone cannot make a still-running child eligible for deletion.
prune_logs() (
    exec 8>"$logs/.retention.lock" || exit 1
    flock --nonblock 8 || exit 0
    now=$(date -u +%s)
    total=0
    directories=()
    sizes=()
    while IFS= read -r -d '' item; do
        directory=${item#* }
        [[ -f "$directory/started" && -f "$directory/.active.lock" ]] || continue
        exec 6<>"$directory/.active.lock" || continue
        if ! flock --nonblock 6; then
            exec 6>&-
            continue
        fi
        modified=${item%% *}
        modified=${modified%%.*}
        if (( now - modified > retention_seconds )); then
            rm -rf -- "$directory" || exit 1
        else
            size=$(du -sb -- "$directory") || exit 1
            size=${size%%$'\t'*}
            directories+=("$directory")
            sizes+=("$size")
            total=$((total + size))
        fi
        exec 6>&-
    done < <(find "$logs" -mindepth 1 -maxdepth 1 -type d -name 'run-*' -printf '%T@ %p\0' | sort -zn)
    for index in "${!directories[@]}"; do
        (( total > budget_bytes )) || break
        rm -rf -- "${directories[index]}" || exit 1
        total=$((total - sizes[index]))
    done
)

started_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
started_seconds=$SECONDS
run=$(mktemp -d "$logs/run-$(date -u +%Y%m%dT%H%M%SZ)-$kind-XXXXXX") || exit 1
exec 7>"$run/.active.lock" || exit 1
flock 7 || exit 1
printf 'run_id=%s\nbot=%s\nstarted_at=%s\nstatus=unfinished\n' "${run##*/}" "$kind" "$started_at" > "$run/started" || exit 1
log="$run/output.log"
write_event() {
    printf '%s run_id=%s bot=%s %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "${run##*/}" "$kind" "$*" | tee -a "$log"
}
finish() {
    local state=$1 code=$2
    write_event "status=$state exit_code=$code elapsed_seconds=$((SECONDS - started_seconds))" || return 1
    printf 'status=%s\nexit_code=%s\nfinished_at=%s\nelapsed_seconds=%s\n' \
        "$state" "$code" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$((SECONDS - started_seconds))" > "$run/result.tmp" || return 1
    mv -- "$run/result.tmp" "$run/result"
}
write_event 'status=started' || exit 1
prune_logs || echo 'Warning: log retention cleanup failed.' >&2
# Opening the shared lock never truncates cached repositories, credentials or logs.
exec 9>"$workspace/.bot.lock" || exit 1
flock --nonblock --conflict-exit-code 75 9
lock_code=$?
if (( lock_code == 75 )); then
    finish skipped 0 || exit 1
    exec 7>&-
    prune_logs || echo 'Warning: log retention cleanup failed.' >&2
    exit 0
elif (( lock_code != 0 )); then
    finish lock_failed "$lock_code" || exit 1
    exit "$lock_code"
fi
write_event 'status=running' || exit 1
# Preserve the bot exit code, rather than the last command in the logging pipeline.
# Redaction happens before either the persistent log or the caller receives output.
"$@" 2>&1 | sed -u -E \
    -e 's#(https?://)[^/@[:space:]]+@#\1[REDACTED]@#g' \
    -e 's/(glpat-|gh[pousr]_|github_pat_)[A-Za-z0-9_.-]+/[REDACTED-TOKEN]/g' \
    -e 's/(Bearer[[:space:]]+)[A-Za-z0-9._~+\/=:-]+/\1[REDACTED]/Ig' \
    -e 's/((private-token|authorization|api[-_]?key|access[-_]?token|password|secret|token)"?[[:space:]]*[:=][[:space:]]*)("[^"]*"|[^[:space:],;]+)/\1[REDACTED]/Ig' \
    | tee -a "$log"
results=("${PIPESTATUS[@]}")
code=${results[0]}
if (( results[1] != 0 || results[2] != 0 )); then
    echo 'Log output failed; the run remains unfinished.' >&2
    (( code != 0 )) || code=1
    exit "$code"
fi
finish exited "$code" || exit 1
exec 9>&-
exec 7>&-
prune_logs || echo 'Warning: log retention cleanup failed.' >&2
exit "$code"
