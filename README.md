# Project P — Puzzle Knight (퍼즐 나이트)

하이브리드 캐주얼 **3매치 퍼즐 + 방치형 RPG**를 목표로 하는 모바일 게임 프로젝트입니다. 저장소는 **Unity 6 클라이언트**와 **ASP.NET Core 서버**로 구성됩니다.

## 한 줄 요약

7×6 퍼즐 보드에서 색상 블록을 매칭하면, 덱에 배치된 히어로의 스킬이 연쇄·콤보와 함께 발동하고 상단 전투 뷰의 아군/적 웨이브와 실시간으로 연동됩니다.

## 기획 배경 (PRD 요약)

- **장르:** 3매치 퍼즐 + 방치형 RPG 하이브리드  
- **플랫폼:** Android 우선, iOS 대응  
- **화면:** 세로 9:16 — 상단 약 40% 전투, 중단 상태 HUD, 하단 약 50% 퍼즐 보드  
- **보드:** 가로 7 × 세로 6 (42칸)  
- **덱:** 최소 3명, 최대 5명. 파티 인덱스(전열~후열)와 블록 색상(빨/노/초/파/보)이 1:1 매핑되며, 사망 시 해당 색 블록은 제거·드롭 중단  
- **전투:** 매칭·연쇄마다 해당 색 히어로 스킬 순차 발동, 콤보에 따른 위력 증폭, 히어별 궁극기 게이지 및 탭 발동  
- **메타:** 성장(레벨/스킬 강화), 에너지·광고·IAP 등 Habby 스타일 BM 방향 (PRD 기준)

자세한 제품 요구사항은 [`Docs/PDR.md`](Docs/PDR.md)를 참고하세요.

## GDD 요약 (문서 폴더)

| 문서 | 내용 |
|------|------|
| [`Docs/GDD/전투 및 퍼즐 로직_2_0.md`](Docs/GDD/전투%20및%20퍼즐%20로직_2_0.md) | 다중 히어로·웨이브 기준 인게임 레이아웃, 블록 매핑, 콤보·궁극기·타겟팅 |
| [`Docs/GDD/히어로 및 스킬 1_0.md`](Docs/GDD/히어로%20및%20스킬%201_0.md) | `HeroData` / `SkillData` JSON 스키마, 스킬 ActionType·TargetScope·상태이상 |
| [`Docs/GDD/스테이지 및 몬스터 시스템 1_0.md`](Docs/GDD/스테이지%20및%20몬스터%20시스템%201_0.md) | `MonsterData` / `StageData`, 웨이브 스폰, 입장 비용·보상·EXP 분배 |

## 저장소 구조

```
Client/     # Unity 6 프로젝트 (URP 2D, UniTask, DOTween, New Input System)
Server/     # ASP.NET Core Web API (Minimal APIs, 스텁/마스터 데이터 API)
Docs/       # PRD, GDD, 태스크 리스트, 일별 작업 로그
```

## 개발 환경

- **클라이언트:** Unity **6000.4.2f1** 권장 (`Client/ProjectSettings/ProjectVersion.txt`)  
- **서버:** **.NET 10** SDK (`Server/ProjectP.Server/ProjectP.Server.csproj`)

### 서버 실행 예시

```bash
cd Server/ProjectP.Server
dotnet run
```

### 클라이언트

`Client` 폴더를 Unity Hub로 열고 에디터 버전을 맞춘 뒤 플레이합니다. 씬·프리팹 연결은 `Docs/Logs`의 일자별 메모를 참고하세요.

## 아키텍처 노트

- **클라이언트:** 보드/전투 로직은 뷰와 분리된 C# 코어를 지향하고, 비동기는 UniTask, UI/연출은 DOTween 등 프로젝트 규칙(`.cursor/rules/unity-client.md`)을 따릅니다.  
- **서버:** UTC 기준 검증·비동기 I/O·Minimal API 등 서버 규칙(`.cursor/rules/dotnet-server.md`)을 따르며, 현재 단계에서는 DTO/스텁 API 중심입니다.

## 라이선스

미정 — 저장소 기본 정책이 정해지면 이 섹션을 업데이트하세요.
