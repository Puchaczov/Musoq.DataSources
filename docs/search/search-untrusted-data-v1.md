# Search untrusted data and process boundary v1

Search treats roots, filenames, ignore rules, request JSON, patterns and
snippets as data. They are never interpreted as shell fragments or automatic
commands. Production Search has no external-process execution path. The
historical test-only ripgrep comparator used `ProcessStartInfo.ArgumentList`,
disabled shell execution, and drained standard output and error concurrently;
that comparator is archived and is not part of the active test or release
gate.

Machine-facing rows and diagnostic locations retain their bounded source value
so a filename such as `-leading-\nfile.txt` remains an exact row identity.
Presentation-facing diagnostic values are separately escaped: C0/C1 control
characters and Unicode terminal-format separators become visible `\\uXXXX`
sequences, and values are bounded before being included in a diagnostic.
Request parser error text therefore cannot inject a terminal line, fake prompt
or unbounded source excerpt. Exact request JSON and snippet content remains
available only through the bounded request/input contract, not copied into
diagnostic messages.

Ignore rules affect candidate eligibility only. A rule is not treated as a
trust boundary, authorization grant or proof that a file is safe; the scope
and containment checks still apply independently.

Source basis: `S07`, `S14`, `S15`, `S18`; implementation scope: `W12-S04`.
