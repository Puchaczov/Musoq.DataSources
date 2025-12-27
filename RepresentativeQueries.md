# Musoq Representative Queries

This document contains a curated set of representative SQL queries demonstrating the power and versatility of Musoq across different data sources. Each query is designed to be simple, easy to understand, yet showcase powerful capabilities.

---

## 📁 File System Queries (`#os`)

### List Files with Size Information
Find all files in a directory with their sizes formatted in human-readable format.

```sql
select 
    Name, 
    ToDecimal(Length) / 1024 as SizeInKB
from #os.files('./directory', true)
where Extension = '.txt'
```

### Calculate SHA256 Hash of Files
Compute cryptographic hashes for file integrity verification.

```sql
select 
    Name, 
    Sha256File() as Hash
from #os.files('./directory', false)
where Extension = '.dll'
```

### Compare Two Directories
Find differences between two directories (added, removed, modified files).

```sql
select 
    SourceFileRelative,
    DestinationFileRelative,
    State
from #os.dirscompare('./source', './destination')
where State <> 'TheSame'
```

---

## 📊 CSV/Separated Values (`#separatedvalues`)

### Basic CSV Query with Aggregation
Analyze banking transactions and calculate monthly income/outcome.

```sql
select 
    ExtractFromDate(OperationDate, 'month') as Month,
    SumIncome(ToDecimal(Money)) as Income,
    SumOutcome(ToDecimal(Money)) as Outcome,
    SumIncome(ToDecimal(Money)) + SumOutcome(ToDecimal(Money)) as Balance
from #separatedvalues.comma('./transactions.csv', true, 0)
group by ExtractFromDate(OperationDate, 'month')
```

### Join Two CSV Files
Join persons with their grades from separate CSV files.

```sql
select 
    persons.Name, 
    persons.Surname, 
    grades.Subject, 
    grades.Grade
from #separatedvalues.comma('./Persons.csv', true, 0) persons 
inner join #separatedvalues.comma('./Gradebook.csv', true, 0) grades 
    on persons.Id = grades.PersonId
```

### Typed CSV Query
Read CSV with explicit column types for proper data handling.

```sql
table Employees {
   Id 'System.Int32',
   Name 'System.String',
   Salary 'System.Decimal'
};
couple #separatedvalues.comma with table Employees as SourceOfEmployees;
select Id, Name, Salary from SourceOfEmployees('./employees.csv', true, 0)
where Salary > 50000
```

---

## 🗂️ JSON Queries (`#json`)

### Query JSON Array
Extract data from a JSON file using a schema definition.

```sql
select 
    Name, 
    Age, 
    Length(Books) as BookCount
from #json.file('./data.json', './data.schema.json')
where Age > 18
```

---

## 📦 Archive Queries (`#archives`)

### List Archive Contents
Read contents of ZIP or TAR archives and extract text content.

```sql
select 
    Key as FileName, 
    IsDirectory,
    (case when IsDirectory = false then GetTextContent() else '' end) as Content
from #archives.file('./archive.zip')
where Key like '%.txt'
```

---

## ⏰ Time Queries (`#time`)

### Generate Date Range
Create a sequence of dates for reporting or analysis.

```sql
select 
    Day, 
    Month, 
    Year, 
    DayOfWeek
from #time.interval('2024-01-01 00:00:00', '2024-12-31 00:00:00', 'days')
```

### Filter Weekend Days
Find only weekend days (Saturday=6, Sunday=0 in DayOfWeek).

```sql
select Day, DayOfWeek
from #time.interval('2024-01-01 00:00:00', '2024-01-31 00:00:00', 'days')
where DayOfWeek = 0 or DayOfWeek = 6
```

---

## 🔧 System Utilities (`#system`)

### Number Range Generation
Generate a sequence of numbers for various purposes.

```sql
select Value 
from #system.range(1, 100)
where Value % 2 = 0
```

### Dual Table for Calculations
Use dual table for single-row calculations.

```sql
select 
    2 + 2 as Sum,
    10 * 5 as Product,
    ToDecimal(7) / 3 as Division
from #system.dual()
```

---

## 🔀 Git Repository Queries (`#git`)

### List Recent Commits
Query commit history with author information.

```sql
select 
    c.Sha,
    c.MessageShort,
    c.Author,
    c.CommittedWhen
from #git.repository('./repo') r 
cross apply r.Commits c
```

### Query Commits Directly
Faster access to commits without full repository context.

```sql
select
    c.Sha,
    c.Author,
    c.Message
from #git.commits('./repo') c
where c.Author = 'john.doe'
```

### List All Branches
Query all branches with their tip commit.

```sql
select
    b.FriendlyName,
    b.IsRemote,
    b.Tip.Sha
from #git.branches('./repo') b
```

### Compare Branches
Find differences between two branches.

```sql
select 
    Difference.Path,
    Difference.ChangeKind
from #git.repository('./repo') repository 
cross apply repository.DifferenceBetween(
    repository.BranchFrom('main'), 
    repository.BranchFrom('feature/my-feature')
) as Difference
```

### List Tags with Annotations
Query all tags in a repository with their metadata.

```sql
select
    t.FriendlyName,
    t.Message,
    t.IsAnnotated,
    t.Commit.Sha
from #git.tags('./repo') t
```

### Track File History
See all changes to a specific file over time.

```sql
select
    h.CommitSha,
    h.Author,
    h.FilePath,
    h.ChangeType
from #git.filehistory('./repo', 'README.md') h
```

### Analyze Branch-Specific Commits
Find commits that are specific to a feature branch.

```sql
with BranchInfo as (
    select
        c.Sha as CommitSha,
        c.Message as CommitMessage,
        c.Author as CommitAuthor
    from #git.repository('./repo') r 
    cross apply r.SearchForBranches('feature/my-feature') b
    cross apply b.GetBranchSpecificCommits(r.Self, b.Self, true) c
)
select CommitSha, CommitMessage, CommitAuthor from BranchInfo
```

### Query Commit Parents
Analyze commit relationships for merge analysis.

```sql
select 
    c.Sha, 
    p.Sha as ParentSha
from #git.commits('./repo') c 
cross apply c.Parents as p
```

---

## 🔬 C# Code Analysis (`#csharp`)

### List All Classes in Solution
Find all classes across a C# solution with their metrics.

```sql
select 
    c.Name,
    c.Namespace,
    c.MethodsCount,
    c.PropertiesCount,
    c.LinesOfCode
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.Documents d 
cross apply d.Classes c
```

### Query All Types in a Project
Quick access to all types (classes, interfaces, enums) in a project.

```sql
select 
    t.Name,
    t.IsClass,
    t.IsInterface,
    t.IsEnum
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.Types t
```

### Find Methods with High Complexity
Identify methods that may need refactoring based on cyclomatic complexity.

```sql
select
    c.Name as ClassName,
    m.Name as MethodName,
    m.CyclomaticComplexity,
    m.LinesOfCode
from #csharp.solution('./MySolution.sln') s 
cross apply s.GetClassesByNames('MyClass') c
cross apply c.Methods m
where m.CyclomaticComplexity > 5
```

### Analyze Method Body Structure
Check for empty methods, stub implementations, and statement counts.

```sql
select
    m.Name,
    m.HasBody,
    m.IsEmpty,
    m.StatementsCount,
    m.BodyContainsOnlyTrivia
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.Documents d 
cross apply d.Classes c
cross apply c.Methods m
where c.Name = 'MyClass'
```

### Analyze Property Accessors
Find auto-properties, init-only setters, and property patterns.

```sql
select
    p.Name,
    p.Type,
    p.IsAutoProperty,
    p.HasGetter,
    p.HasSetter,
    p.HasInitSetter
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.Documents d 
cross apply d.Classes c
cross apply c.Properties p
where c.Name = 'MyClass'
```

### Find References to a Class
Locate all usages of a specific class across the solution.

```sql
select 
    r.Name,
    rd.StartLine,
    rd.StartColumn,
    rd.EndLine,
    rd.EndColumn
from #csharp.solution('./MySolution.sln') s
cross apply s.GetClassesByNames('MyClass') c
cross apply s.FindReferences(c.Self) rd
cross apply rd.ReferencedClasses r
```

### Query Interface Definitions
List interfaces with their methods and properties.

```sql
select
    i.Name,
    i.FullName,
    i.Namespace,
    i.BaseInterfaces,
    i.Methods,
    i.Properties
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects pr 
cross apply pr.Documents d 
cross apply d.Interfaces i
```

### Analyze Enums
List enums with their members.

```sql
select
    e.Name,
    e.FullName,
    e.Namespace,
    e.Members
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects pr 
cross apply pr.Documents d 
cross apply d.Enums e
```

### Query Project References
List all project-to-project references.

```sql
select
    p.Name as ProjectName,
    ref.Name as ReferencedProject
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.ProjectReferences ref
```

### Query Library References
List all library/assembly references in projects.

```sql
select
    p.Name as ProjectName,
    lib.Name as LibraryName,
    lib.Version,
    lib.Location
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.LibraryReferences lib
```

### Query NuGet Packages
List all NuGet packages with license information.

```sql
select 
    p.Name as ProjectName,
    np.Id as PackageId,
    np.Version,
    np.License,
    np.Authors,
    np.IsTransitive
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.GetNugetPackages(false) np
```

### Analyze Class Attributes
Find classes decorated with specific attributes.

```sql
select
    c.Name,
    a.Name as AttributeName,
    a.ConstructorArguments
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects pr 
cross apply pr.Documents d 
cross apply d.Classes c
cross apply c.Attributes a
```

### Query Method Parameters
Analyze method parameters with their modifiers.

```sql
select
    m.Name as MethodName,
    p.Name as ParamName,
    p.Type,
    p.IsOptional,
    p.IsParams,
    p.IsRef,
    p.IsOut
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects pr 
cross apply pr.Documents d 
cross apply d.Classes c
cross apply c.Methods m
cross apply m.Parameters p
```

### Calculate Lack of Cohesion
Analyze class design metrics.

```sql
select 
    c.Name,
    c.MethodsCount,
    c.FieldsCount,
    c.LackOfCohesion,
    c.InheritanceDepth
from #csharp.solution('./MySolution.sln') s 
cross apply s.Projects p 
cross apply p.Documents d 
cross apply d.Classes c
where c.MethodsCount > 2
```

---

## 🔗 Combined Data Source Queries

### Analyze Git Repositories from File System
Discover and analyze multiple Git repositories.

```sql
with GitRepos as (
    select 
        dir.Parent.Name as RepoName,
        dir.FullName as GitPath
    from #os.directories('./projects', true) dir
    where dir.Name = '.git'
)
select 
    r.RepoName,
    Count(c.Sha) as CommitCount
from GitRepos r 
cross apply #git.repository(r.GitPath) repo 
cross apply repo.Commits c
group by r.RepoName
order by CommitCount desc
```

### Diff Files with Hash Comparison
Compare directories using file hashes to detect modifications.

```sql
with SourceFiles as (
    select GetRelativePath('./source') as RelPath, Sha256File() as Hash 
    from #os.files('./source', true)
), 
TargetFiles as (
    select GetRelativePath('./target') as RelPath, Sha256File() as Hash 
    from #os.files('./target', true)
)
select 
    s.RelPath,
    (case when s.Hash <> t.Hash then 'modified' else 'same' end) as Status
from SourceFiles s 
inner join TargetFiles t on s.RelPath = t.RelPath
```

---

## Notes

- All queries use standard SQL syntax with Musoq-specific extensions
- Table functions use `#datasource.table()` syntax
- Cross apply enables joining hierarchical data
- CTEs (Common Table Expressions) are fully supported
- Queries can combine multiple data sources in a single statement

For more information, visit the [Musoq GitHub repository](https://github.com/Puchaczov/Musoq).
