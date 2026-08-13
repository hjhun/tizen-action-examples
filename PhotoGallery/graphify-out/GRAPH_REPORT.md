# Graph Report - /home/hjhun/samba/workspace/tizen-action-examples/PhotoGallery  (2026-08-11)

## Corpus Check
- 38 files · ~18,693 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 210 nodes · 251 edges · 49 communities detected
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 18 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]

## God Nodes (most connected - your core abstractions)
1. `TizenActionPhoto` - 16 edges
2. `PhotoGalleryService` - 15 edges
3. `GalleryInteractionReducer` - 13 edges
4. `ServiceBase` - 9 edges
5. `PhotoGallery 아키텍처 및 제품 계약` - 9 edges
6. `PhotoGallery 제품 요구사항과 Architect Gate` - 8 edges
7. `PhotoLibraryRefreshCoordinator` - 7 edges
8. `TizenEntity` - 6 edges
9. `UnitMap` - 6 edges
10. `PhotoGallery UI parity ledger` - 6 edges

## Surprising Connections (you probably didn't know these)
- `MediaContentPhotoLibrary` --inherits--> `IPhotoLibrary`  [EXTRACTED]
  /home/hjhun/samba/workspace/tizen-action-examples/PhotoGallery/src/PhotoGallery.Persistence/MediaContentPhotoLibrary.cs →   _Bridges community 3 → community 10_

## Communities

### Community 0 - "Community 0"
Cohesion: 0.1
Nodes (5): ServiceBase, TizenActionPhoto, UnitMap, PhotoQueryService, StubBase

### Community 1 - "Community 1"
Cohesion: 0.18
Nodes (3): PhotoActionDiagnostics, PhotoGalleryService, PhotoResolver

### Community 2 - "Community 2"
Cohesion: 0.16
Nodes (5): TizenEntity, TizenEntityPhoto, TizenEntityPresentation, TizenEntityQuery, TizenEntityStatus

### Community 3 - "Community 3"
Cohesion: 0.13
Nodes (6): IPhotoLibrary, PhotoGalleryActionProviderHost, PhotoGalleryProviderState, UnavailablePhotoLibrary, BlockingThenResultLibrary, SequencedLibrary

### Community 4 - "Community 4"
Cohesion: 0.12
Nodes (8): Exception, CallbackBase, LocalExecution, PhotoGalleryActionProvider, RemoteException, RPCPort, Stub, Unit

### Community 5 - "Community 5"
Cohesion: 0.23
Nodes (1): GalleryInteractionReducer

### Community 6 - "Community 6"
Cohesion: 0.25
Nodes (3): IDisposable, IPhotoLibrary, PhotoLibraryRefreshCoordinator

### Community 7 - "Community 7"
Cohesion: 0.18
Nodes (10): 1. 범위와 사용자 가치, 2. 발견 근거와 계약, 3. 실제 미디어 경계와 선택지, 4. 계층과 동시성, 5. One UI 제품 흐름과 CX, 6. NUI scaling, annotation, A2UI, 7. 검증 가능한 acceptance와 단계, 8. 다음 구현 slice (+2 more)

### Community 8 - "Community 8"
Cohesion: 0.22
Nodes (8): 1. 제품 목표와 범위, 2. 근거와 One UI 적응, 3. 설계 선택과 경계, 4. 기능·상태·입력 acceptance, 5. privacy와 Presentation, 6. 완료 gate와 순서, 7. 현재 evidence와 다음 slice, PhotoGallery 제품 요구사항과 Architect Gate

### Community 9 - "Community 9"
Cohesion: 0.22
Nodes (8): A2UI current-state contract, Authoritative reference sources inspected, Capture and comparison ledger, Evidence boundary, Executable sample, PhotoGallery UI parity ledger, Reference audit and Tizen adaptation — 2026-08-09, Sample state inventory and implementation mapping

### Community 10 - "Community 10"
Cohesion: 0.33
Nodes (2): MediaContentPhotoLibrary, Create()

### Community 11 - "Community 11"
Cohesion: 1.0
Nodes (0): 

### Community 12 - "Community 12"
Cohesion: 1.0
Nodes (0): 

### Community 13 - "Community 13"
Cohesion: 1.0
Nodes (0): 

### Community 14 - "Community 14"
Cohesion: 1.0
Nodes (0): 

### Community 15 - "Community 15"
Cohesion: 1.0
Nodes (0): 

### Community 16 - "Community 16"
Cohesion: 1.0
Nodes (0): 

### Community 17 - "Community 17"
Cohesion: 1.0
Nodes (0): 

### Community 18 - "Community 18"
Cohesion: 1.0
Nodes (0): 

### Community 19 - "Community 19"
Cohesion: 1.0
Nodes (0): 

### Community 20 - "Community 20"
Cohesion: 1.0
Nodes (0): 

### Community 21 - "Community 21"
Cohesion: 1.0
Nodes (0): 

### Community 22 - "Community 22"
Cohesion: 1.0
Nodes (0): 

### Community 23 - "Community 23"
Cohesion: 1.0
Nodes (0): 

### Community 24 - "Community 24"
Cohesion: 1.0
Nodes (0): 

### Community 25 - "Community 25"
Cohesion: 1.0
Nodes (0): 

### Community 26 - "Community 26"
Cohesion: 1.0
Nodes (0): 

### Community 27 - "Community 27"
Cohesion: 1.0
Nodes (0): 

### Community 28 - "Community 28"
Cohesion: 1.0
Nodes (0): 

### Community 29 - "Community 29"
Cohesion: 1.0
Nodes (0): 

### Community 30 - "Community 30"
Cohesion: 1.0
Nodes (0): 

### Community 31 - "Community 31"
Cohesion: 1.0
Nodes (0): 

### Community 32 - "Community 32"
Cohesion: 1.0
Nodes (0): 

### Community 33 - "Community 33"
Cohesion: 1.0
Nodes (0): 

### Community 34 - "Community 34"
Cohesion: 1.0
Nodes (0): 

### Community 35 - "Community 35"
Cohesion: 1.0
Nodes (0): 

### Community 36 - "Community 36"
Cohesion: 1.0
Nodes (0): 

### Community 37 - "Community 37"
Cohesion: 1.0
Nodes (1): net8.0

### Community 38 - "Community 38"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

### Community 39 - "Community 39"
Cohesion: 1.0
Nodes (1): net8.0

### Community 40 - "Community 40"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

### Community 41 - "Community 41"
Cohesion: 1.0
Nodes (1): net8.0

### Community 42 - "Community 42"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

### Community 43 - "Community 43"
Cohesion: 1.0
Nodes (1): net8.0

### Community 44 - "Community 44"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

### Community 45 - "Community 45"
Cohesion: 1.0
Nodes (1): net8.0

### Community 46 - "Community 46"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

### Community 47 - "Community 47"
Cohesion: 1.0
Nodes (1): net8.0

### Community 48 - "Community 48"
Cohesion: 1.0
Nodes (1): Microsoft.NET.Sdk

## Knowledge Gaps
- **35 isolated node(s):** `RPCPort`, `PhotoGalleryActionProvider`, `Stub`, `net8.0`, `Microsoft.NET.Sdk` (+30 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **Thin community `Community 11`** (2 nodes): `PhotoRecord.cs`, `Create()`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 12`** (1 nodes): `PhotoGallery.UseCases.Tests.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 13`** (1 nodes): `PhotoGallery.UseCases.Tests.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 14`** (1 nodes): `PhotoGallery.UseCases.Tests.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 15`** (1 nodes): `PhotoGallery.UseCases.Tests.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 16`** (1 nodes): `Program.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 17`** (1 nodes): `PhotoGallery.Domain.Tests.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 18`** (1 nodes): `PhotoGallery.Domain.Tests.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 19`** (1 nodes): `PhotoGallery.Domain.Tests.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 20`** (1 nodes): `PhotoGallery.Domain.Tests.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 21`** (1 nodes): `PhotoGallery.Persistence.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 22`** (1 nodes): `PhotoGallery.Persistence.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 23`** (1 nodes): `PhotoGallery.Persistence.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 24`** (1 nodes): `PhotoGallery.Persistence.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 25`** (1 nodes): `PhotoGallery.Domain.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 26`** (1 nodes): `PhotoGallery.Domain.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 27`** (1 nodes): `PhotoGallery.Domain.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 28`** (1 nodes): `PhotoGallery.Domain.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 29`** (1 nodes): `PhotoGallery.ActionProvider.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 30`** (1 nodes): `PhotoGallery.ActionProvider.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 31`** (1 nodes): `PhotoGallery.ActionProvider.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 32`** (1 nodes): `PhotoGallery.ActionProvider.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 33`** (1 nodes): `PhotoGallery.UseCases.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 34`** (1 nodes): `PhotoGallery.UseCases.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 35`** (1 nodes): `PhotoGallery.UseCases.AssemblyInfo.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 36`** (1 nodes): `PhotoGallery.UseCases.GlobalUsings.g.cs`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 37`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 38`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 39`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 40`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 41`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 42`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 43`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 44`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 45`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 46`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 47`** (1 nodes): `net8.0`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.
- **Thin community `Community 48`** (1 nodes): `Microsoft.NET.Sdk`
  Too small to be a meaningful cluster - may be noise or needs more connections extracted.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TizenActionPhoto` connect `Community 0` to `Community 3`, `Community 4`?**
  _High betweenness centrality (0.119) - this node is a cross-community bridge._
- **Why does `Create()` connect `Community 10` to `Community 1`?**
  _High betweenness centrality (0.103) - this node is a cross-community bridge._
- **Why does `GalleryInteractionReducer` connect `Community 5` to `Community 10`?**
  _High betweenness centrality (0.074) - this node is a cross-community bridge._
- **What connects `RPCPort`, `PhotoGalleryActionProvider`, `Stub` to the rest of the system?**
  _35 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Community 3` be split into smaller, more focused modules?**
  _Cohesion score 0.13 - nodes in this community are weakly interconnected._
- **Should `Community 4` be split into smaller, more focused modules?**
  _Cohesion score 0.12 - nodes in this community are weakly interconnected._