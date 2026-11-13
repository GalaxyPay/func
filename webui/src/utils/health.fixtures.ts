// Builders for health data in tests: everything empty unless a test sets it.
import type { HealthPoint, NodeHealth } from "@/types";

export function point(values: Partial<HealthPoint> = {}): HealthPoint {
  return {
    t: 0,
    rounds: 0,
    roundMsMax: 0,
    votes: 0,
    proposals: 0,
    wins: 0,
    blocks: 0,
    arrivalMsMax: 0,
    recoveries: 0,
    connects: 0,
    disconnects: 0,
    connectFails: 0,
    warnings: 0,
    errors: 0,
    bytesIn: 0,
    bytesOut: 0,
    dropped: 0,
    ...values,
  };
}

export function nodeHealth(values: Partial<NodeHealth> = {}): NodeHealth {
  return {
    source: "ok",
    metrics: false,
    lastRound: 0,
    bucketSeconds: 900,
    points: [],
    peers: [],
    notes: [],
    ...values,
  };
}
