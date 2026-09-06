# Search scope and path contract v1

Status: proposed design contract for `W01-S03`. It defines how a future
Search scan resolves and explains its eligible paths. It is not evidence that
the scanner is installed or that any filesystem has been traversed.

The canonical table-driven corpus is
[`search-scope-contract-v1.json`](search-scope-contract-v1.json). The
independent test is
[`Test-SearchScopeSemantics.ps1`](../../scripts/search/Test-SearchScopeSemantics.ps1).

## Defaults

| Policy | Default |
|---|---|
| Recursion | `true` |
| Path comparison | filesystem-native, recorded in the fingerprint |
| Include globs | empty; all otherwise eligible files are candidates |
| Exclude globs | empty |
| Repository ignores | respect `.gitignore`, `.ignore` and `.rgignore` |
| Global ignores | disabled unless explicitly enabled |
| Hidden entries | exclude |
| Symbolic links/reparse points | do not follow |
| Hard links | distinct paths; no file-ID deduplication |
| Inaccessible root/entry | typed failure by default |
| Missing root | typed failure before an empty scan can be reported |

All options are part of the resolved scope, not ambient behavior. Actual
absolute paths and loaded rule hashes belong in ignored local evidence; shared
documents use synthetic roots.

## Root and containment

Relative roots resolve against the query working directory captured by the
execution request. Absolute roots are normalized without changing the
requested filesystem object. A file root has one candidate; a directory root
is the traversal boundary. A missing root fails before scanning.

Containment is a security decision and runs before convenience filtering. For
lexical containment, normalize separators and dot segments and require the
candidate to equal the root or have the root plus a separator as prefix. An
outside path cannot be made safe by an include, exclude, ignore or hidden-file
rule. When links are followed explicitly, resolve the target and require
physical containment under the resolved physical root as well.

`Path` remains a path identity. A hard-linked `a.txt` and `alias.txt` are two
path candidates even if they share a file identity; this contract does not
silently trade path evidence for inode deduplication.

## Traversal and filters

The evaluation order is:

1. security containment;
2. include allow-list;
3. exclude deny-list;
4. repository/global ignore rules;
5. hidden, link and special-entry policies.

If `include` is non-empty, a file must match at least one include glob. A
matching descendant is sufficient reason to continue traversing an unmatched
directory. A file extension pattern such as `*.cs` can filter files but cannot
prune a directory merely because the directory name does not have that
extension. Only a security rejection or an explicit directory-only exclusion
may prune a subtree. Excludes win over includes.

Non-recursive mode inspects only direct eligible files in the root directory.
Special entries are named policy decisions and are never silently treated as
regular files. Candidate manifests use normalized relative paths sorted for
the fingerprint; this is not an SQL result-order guarantee.

## Ignore precedence and negation

Repository ignore sources are ordered from lowest to highest precedence:

1. `.gitignore`;
2. `.ignore`;
3. `.rgignore`.

The configured global ignore source is disabled by default. Higher-precedence
sources win, and later matching rules within a source win. A negated rule can
re-include a path within its source, but it cannot cross the containment
boundary. A directory pruned by an ancestor rule is not reopened solely by a
descendant negation; the parent must first be re-included.

Every loaded ignore file contributes its normalized path, precedence and
SHA-256 content hash to the scope fingerprint. This prevents a changing user
or repository ignore file from masquerading as a scanner result change.

## Hidden entries, links and access errors

Hidden files/directories are excluded by default and may be enabled explicitly
after security checks. Symbolic links and reparse-point directories are not
followed by default. An explicit follow policy must enforce physical
containment and maintain a visited physical-directory set to stop cycles.

The root being inaccessible is a failure. A descendant access error also fails
by default and records the path and traversal phase. An explicit skip policy
may be added only if the audit result exposes the skipped count and does not
claim an exhaustive answer.

## Scope fingerprint

The fingerprint is the SHA-256 of canonical UTF-8 JSON: object keys sorted
ordinally, arrays retaining semantic order, compact separators, and no hidden
machine-specific fields. It includes:

- contract version, requested root, resolved root identity, root kind and
  query working directory;
- effective filesystem path comparison;
- recursion, include/exclude lists and ignore settings;
- loaded ignore paths, precedence and content hashes;
- hidden, link, hard-link and inaccessible-entry policies;
- the sorted candidate decision manifest and per-path reasons.

The explanation accompanying the fingerprint contains the normalized policy,
loaded-rule hashes, candidate counts and decision reasons needed to reproduce
it. A fingerprint is therefore an explanation handle, not just an opaque
cache key.

## Required table-driven cases

The corpus and harness cover nested ignore rules with negation, an ignored
parent that blocks a descendant negation, lexical versus physical containment,
case-sensitive versus case-insensitive path matching, extension filtering that
must not prune a descendant directory, hard-link path identity and an
inaccessible descendant. The case-sensitive fixture is logical and portable;
the runtime must record the actual filesystem comparison mode rather than
assuming that the host OS name alone decides it.

## Authority and boundary

The contract follows the Runtime-v2 source boundary: scope resolution is
separate from typed row production and SQL ordering. It also records the
relevant ripgrep-style distinction between repository/global ignore rules,
hidden files and symlink following, while making product policy explicit rather
than inheriting ambient process state.

Completion and failure outcomes are finalized by `W01-S04`; request schema
validation is finalized by `W01-S05`. No backend or convenience ignore is
allowed to weaken security containment.

Source references: `S14`, `S15`, `S20`, `S22`, `S23`.
