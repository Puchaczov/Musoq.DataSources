# Search delivery latency results v1

This is the `W16-S03` latency-boundary record for the Search datasource and
the in-process compiled-query API. The machine-readable record is
[`search-delivery-latency-results-v1.json`](search-delivery-latency-results-v1.json).
The raw seven-trial output is retained under
[`docs/campaigns/search/evidence/W16-S03`](../campaigns/search/evidence/W16-S03).

## Observed boundaries

The Windows x64 Release runner used a deterministic two-file, 25-byte fixture
with three `TODO` rows. It excluded one warm-up and retained seven complete
trials for each of three cells:

- `direct-source-fresh`: first `RowSource.Chunks.MoveNext` median 0.5793 ms;
  the post-first-chunk interval is 0.0616 ms and terminal enumeration median
  is 0.6416 ms.
- `compiled-query-compile-cache-miss`: fresh generated assembly/query instance
  per trial, alternating the equivalent `TODO` and `TO` query patterns;
  compile median 52.5140 ms, evaluator table return median 1.4573 ms, first
  client-visible row from the returned table median 8.8749 ms, and terminal
  median 8.9675 ms. The post-return-to-first-row interval is 7.4563 ms.
- `compiled-query-reuse`: one compiled query instance reused serially after a
  93.3155 ms setup compile; evaluator table return median 0.0128 ms, first
  client-visible row median 0.9343 ms and terminal median 0.9354 ms. The
  post-return-to-first-row interval is 0.9215 ms.

The raw record keeps compile, evaluator table-return, first table-row and
terminal timestamps separately, together with the non-cumulative intervals
needed for additive accounting. A source chunk is an internal producer
boundary; it is not a client-visible result. The table enumerator is the
closest boundary observable from this repository, and it still excludes
serialization and transport.

## Additive budget and residuals

The observations support additive accounting for the in-process pieces only:
compile/cache-miss work, evaluator return, table first-row access and terminal
completion. Filesystem cache state was not controlled, so every cell is labeled
`unknown`; no cold or controlled-warm filesystem claim is made.

Cold CLI end-to-end and warm-service end-to-end timing were not run. Their
startup, configuration, ready-service, transport and client-output boundaries
belong to the Core/host owners and are explicitly marked `not_run` in the
record. No engine or host change is proposed here. In particular, this scope
does not advertise streaming or treat early `RowSource` chunks as early client
output.

No package was published, pushed, released or installed, and no sibling
repository was edited.
