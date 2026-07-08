# ADR-003: Local-only privacy model

## Status

Accepted

## Context

The intended app is a personal utility. Network, accounts, analytics, ads, and telemetry add risk and no value.

## Decision

BudsMonitor performs no network calls in normal operation and stores all state locally.

## Consequences

Positive:

- Simple trust model.
- Easier distribution.
- No server dependency.

Negative:

- No cloud sync.
- No remote crash reporting.
- Diagnostics must be manually exported.
