# Search correctness matrix v1

This matrix records the current owning-repository correctness evidence for
oracle, property, differential, planner and integration paths. It distinguishes
the Windows x64 run from unavailable Linux/macOS/musl profiles and calls the
existing deterministic randomized tests seeded property coverage rather than
inventing a full fuzzing result. The machine-readable inventory is
[`search-correctness-matrix-v1.json`](search-correctness-matrix-v1.json).

The current Search project covers independent text/byte oracles, generated
property cases, managed-oracle and planner differential checks,
compiled source-engine queries, Parse composition, byte coordinates, terminal
outcomes, residual TAKE/order behavior and concurrent audit isolation. The
full owning Release suite is the completion regression gate.

The managed-versus-ripgrep checks in the archived parity records are retained
for provenance; their external comparator is not part of the active test or
release gate.

Three required platform profiles remain explicitly unavailable in this
checkout, and no separate fuzz engine exists. Those are claim blockers, not
silent skips. The matrix contains no benchmark result rows; performance
qualification remains W16-S02 and later.
