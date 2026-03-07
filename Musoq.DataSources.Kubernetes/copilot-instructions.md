# Kubernetes plugin guide

## Purpose
- Exposes many cluster resources including pods, services, deployments, replicasets, jobs, cronjobs, statefulsets, daemonsets, logs, and events.

## Read first
- `KubernetesSchema.cs`
- `KubernetesApi.cs`
- `IKubernetesApi.cs`
- resource folders like `Pods/`, `Services/`, `Deployments/`, `Nodes/`, and `Events/`

## Architecture map
- The project is organized as one folder per Kubernetes resource type.
- Workload-style resources live in folders like `Deployments/`, `ReplicaSets/`, `StatefulSets/`, `DaemonSets/`, `Jobs/`, and `CronJobs/`.
- Core and cluster resources live in `Pods/`, `PodContainers/`, `PodLogs/`, `Services/`, `Nodes/`, `Configmaps/`, `Secrets/`, `Events/`, `Ingresses/`, `PersistentVolumes/`, and `PersistentVolumeClaims/`.
- Most folders follow the same quartet: `*Entity`, `*Source`, `*SourceHelper`, and `*Table`.
- `KubernetesLibrary.cs` is the SQL-helper surface and should stay small compared with the folder-local entity/source code.

## Schema and overload conventions
- `KubernetesSchema` has two constructors: the public path creates a real client through the client factory, while the internal path accepts `IKubernetesApi` for tests and mocking.
- Most data sources are zero-argument sources.
- The main explicit overload exceptions are `podlogs(podName, containerName, namespaceName)` and `events()` / `events(namespaceName)`.
- Adding a new resource requires updating all schema switch points: registration, `GetTableByName()`, `GetRowSource()`, and constructor metadata.
- XML docs and raw constructor metadata together define the public `desc #kubernetes` contract.

## Patterns to preserve
- Follow the folder-per-resource pattern.
- Most surfaces are static tables/sources; notable constructor exceptions like `podlogs(...)` and `events(namespace)` should stay explicit.
- Keep API access behind `IKubernetesApi` and keep row shape in resource-specific entities/tables.
- `KubernetesSchema.cs` XML docs are extensive and drive discoverability.

## API and config boundaries
- Keep direct Kubernetes SDK calls inside `KubernetesApi.cs`.
- `KubernetesSchema.cs` should only route names and parameters to the correct resource sources.
- Each `*Source` owns retrieval plus mapping from `V1*` Kubernetes objects into plugin entities.
- The only plugin-specific environment hook is `MUSOQ_KUBERNETES_CONFIG_FILE`.
- That variable is treated as raw kubeconfig content, not as a filesystem path; when absent, the plugin falls back to default kubeconfig resolution.

## Integrations
- `KubernetesClient`
- `Docker.DotNet`
- Optional environment variable: `MUSOQ_KUBERNETES_CONFIG_FILE`

## Common pitfalls
- Do not forget to update every schema switch when adding a resource; missing one usually breaks runtime resolution or `desc` output.
- `podlogs` uses a parameter-order bridge between the schema-facing signature and the API wrapper method; preserve that mapping carefully.
- Several resource mappers assume non-null nested Kubernetes objects; sparse API responses can expose nullability gaps.
- Tests and discoverability are XML-doc-sensitive, so code and schema docs can drift if updated separately.
- Avoid copying suspicious inheritance or generic-type quirks between resource folders without checking whether they are intentional.

## Safe extension points
- Add a new resource by following the existing `Entity` + `Source` + `SourceHelper` + `Table` pattern in a dedicated folder.
- Add the API method to `IKubernetesApi` / `KubernetesApi` first, then wire the schema switches.
- Add SQL-callable helper methods in `KubernetesLibrary.cs` when behavior belongs on top of already-fetched entities.
- If a helper needs Kubernetes metadata, implementing or reusing `IWithObjectMetadata` is the low-risk pattern.

## Validate with
- `Musoq.DataSources.Kubernetes.Tests/KubernetesTests.cs`
- `Musoq.DataSources.Kubernetes.Tests/KubernetesSchemaDescribeTests.cs`

## Most representative tests
- `KubernetesSchemaDescribeTests.cs` is the authority for method inventory, parameter signatures, overload counts, and unknown-method behavior.
- `KubernetesTests.cs` is the main projection contract across resource types; the `pods`, `podcontainers`, `podlogs`, `events`, and `deployments` cases are the best anchors.
- `KubernetesPlaygroundTests.cs` is exploratory and should not be treated as the formal contract.