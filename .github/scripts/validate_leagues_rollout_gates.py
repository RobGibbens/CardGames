#!/usr/bin/env python3

import json
import sys
from pathlib import Path


REQUIRED_ENDPOINTS = {"join", "request", "approve"}
REQUIRED_MONITORS = {
    "duplicate_active_membership",
    "orphaned_role_state",
    "unauthorized_mutation_success",
    "join_failure_reason_spike",
}
REQUIRED_STAGES = ["internal", "beta", "ga"]
REQUIRED_CRITERIA = {
    "p0_ci_gate_green",
    "slo_within_budget",
    "integrity_monitors_green",
}


def fail(message: str) -> None:
    print(f"::error::{message}")
    raise SystemExit(1)


def validate(path: Path) -> None:
    if not path.exists():
        fail(f"Rollout gate manifest not found: {path}")

    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        fail(f"Invalid JSON in {path}: {exc}")

    if payload.get("version") != 1:
        fail("rollout gates manifest 'version' must be 1")

    feature_flag = payload.get("featureFlag")
    if not isinstance(feature_flag, str) or not feature_flag.strip():
        fail("rollout gates manifest requires non-empty 'featureFlag'")

    slo = payload.get("slo")
    if not isinstance(slo, dict):
        fail("rollout gates manifest requires object 'slo'")

    if slo.get("evaluationWindow") != "24h":
        fail("slo.evaluationWindow must be '24h'")

    endpoints = slo.get("endpoints")
    if not isinstance(endpoints, list):
        fail("slo.endpoints must be an array")

    endpoint_names = set()
    for endpoint in endpoints:
        if not isinstance(endpoint, dict):
            fail("each slo endpoint must be an object")

        name = endpoint.get("name")
        if not isinstance(name, str):
            fail("each slo endpoint requires string 'name'")

        endpoint_names.add(name)

        p95 = endpoint.get("p95LatencyMs")
        if not isinstance(p95, int) or p95 <= 0:
            fail(f"endpoint '{name}' has invalid p95LatencyMs")

        error_rate = endpoint.get("errorRatePercent")
        if not isinstance(error_rate, (int, float)) or error_rate < 0 or error_rate > 100:
            fail(f"endpoint '{name}' has invalid errorRatePercent")

    missing_endpoints = REQUIRED_ENDPOINTS - endpoint_names
    if missing_endpoints:
        fail(f"missing required SLO endpoints: {sorted(missing_endpoints)}")

    monitors = payload.get("integrityMonitors")
    if not isinstance(monitors, list):
        fail("integrityMonitors must be an array")

    monitor_set = {x for x in monitors if isinstance(x, str)}
    missing_monitors = REQUIRED_MONITORS - monitor_set
    if missing_monitors:
        fail(f"missing required integrity monitors: {sorted(missing_monitors)}")

    stages = payload.get("rolloutStages")
    if not isinstance(stages, list):
        fail("rolloutStages must be an array")

    if [s.get("name") for s in stages if isinstance(s, dict)] != REQUIRED_STAGES:
        fail("rolloutStages must appear in order: internal, beta, ga")

    previous_cohort = -1
    for stage in stages:
        if not isinstance(stage, dict):
            fail("each rollout stage must be an object")

        name = stage.get("name")
        cohort = stage.get("maxCohortPercent")
        stable_hours = stage.get("minStableHours")
        criteria = stage.get("requiredCriteria")

        if not isinstance(cohort, int) or cohort <= 0 or cohort > 100:
            fail(f"stage '{name}' has invalid maxCohortPercent")

        if cohort <= previous_cohort:
            fail("rollout stage maxCohortPercent values must strictly increase")
        previous_cohort = cohort

        if not isinstance(stable_hours, int) or stable_hours < 24:
            fail(f"stage '{name}' has invalid minStableHours; minimum is 24")

        if not isinstance(criteria, list):
            fail(f"stage '{name}' requiredCriteria must be an array")

        criteria_set = {x for x in criteria if isinstance(x, str)}
        missing_criteria = REQUIRED_CRITERIA - criteria_set
        if missing_criteria:
            fail(f"stage '{name}' missing criteria: {sorted(missing_criteria)}")

    if stages[-1].get("maxCohortPercent") != 100:
        fail("final rollout stage must target 100% cohort")

    print(f"Rollout gates manifest validated: {path}")


def main() -> None:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(".github/ops/leagues-rollout-gates.json")
    validate(path)


if __name__ == "__main__":
    main()