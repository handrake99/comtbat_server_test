# Combat Server
크로매틱소울 AFK Raid의 실시간 전투 서버
MO 실시간 서버

## Tech Stack

### Language & Runtime
- **C#** (.NET Framework)
- **Windows Service** 지원 (콘솔/서비스 모드)

### Data & Communication
- **StackExchange.Redis** - Redis 클라이언트 라이브러리
- **Newtonsoft.Json** - JSON 직렬화/역직렬화

## Features

### 직렬화 기반 스레드 안전성 보장
각 서버 객체(`Room`, `RoomManager`, `RedisManager` 등)는 `CorgiSerializer`를 통해 메서드 호출을 직렬화합니다. `Interlocked` 연산과 `ConcurrentQueue`를 활용해 동시성 제어를 구현했으며, 모든 상태 변경은 단일 스레드에서 순차 실행되어 경쟁 조건을 방지합니다.

### Room 기반 멀티플레이어 관리
`RoomManager`가 `ConcurrentDictionary<string, Room>`으로 Room 인스턴스를 관리합니다. 각 Room은 독립적인 게임 세션으로 동작하며, 파티 플레이, 인스턴스 던전, 월드 보스, 아레나 등 다양한 게임 모드를 지원합니다. Room 생명주기는 자동 관리되며, 빈 Room은 일정 시간 후 자동 정리됩니다.

### Redis 큐 기반 비동기 명령 처리
웹 서버와의 통신은 Redis 큐를 통해 비동기로 처리됩니다. 수신 큐(`queue-web-to-combat-{index}`)에서 명령을 폴링하고, 전송 큐(`queue-combat-to-web-{index}`)로 결과를 전송합니다. Command 패턴으로 각 게임 이벤트를 캡슐화하여 확장성을 확보했습니다.

### 게임 틱 시스템
100ms 간격의 고정 틱으로 게임 로직을 업데이트합니다. 메인 루프에서 틱 간격을 체크하고, `RoomManager`와 `StatDataManager`에 틱 이벤트를 전파합니다. 각 Room은 `ThreadPool`을 통해 병렬로 틱을 처리하여 성능을 최적화했습니다.

### 서버 상태 모니터링 및 Keep-Alive
`AliveSignalManager`가 주기적으로 Redis에 핑을 전송하고, 응답 지연을 모니터링합니다. 응답 지연이 임계값을 초과하면 서버를 안전하게 종료하여 장애 전파를 방지합니다. 서버 상태와 Room 목록은 Redis에 주기적으로 기록됩니다.

### 게임 데이터 버전 관리
`ServerGameDataManager`가 Redis에서 최신 리비전 정보를 조회하고, 필요한 게임 데이터 파일을 자동으로 다운로드합니다. 리비전별로 게임 데이터를 관리하여 무중단 업데이트를 지원합니다.

## 🏗️ Server Architecture

이 프로젝트는 **Web Server**와 **Dedicated Server**로 구성된 하이브리드 아키텍처를 따릅니다.
클라이언트는 웹 서버를 통해 인증 및 매치메이킹을 수행한 후, 배정된 데디케이티드 서버에 접속하여 실시간 게임을 진행합니다.

### 📊 Architecture Diagram

```mermaid
flowchart TD
%% --- 스타일 정의 (다크 모드 최적화) ---
    %% Client: 깊은 보라/자주색 (눈이 편안함)
    classDef client fill:#4b2e3f,stroke:#aaa,stroke-width:2px,color:#eee
    %% Server: 깊은 네이비 (차분함)
    classDef server fill:#283d56,stroke:#aaa,stroke-width:2px,color:#eee
    %% Storage: 깊은 올리브/브라운 (안정감)
    classDef storage fill:#544a2d,stroke:#aaa,stroke-width:2px,color:#eee

    %% Subgraph 스타일 (점선 테두리)
    classDef subgraph_style fill:none,stroke:#666,stroke-width:1px,stroke-dasharray: 5 5,color:#ccc
    
    %% Subgraph 타이틀 색상 조정
    classDef subgraph_style fill:none,stroke:#666,stroke-width:1px,color:#ccc,stroke-dasharray: 5 5;
    %% -----------------------------------

    %% Nodes (수정됨: 끝부분 콜론 제거 및 텍스트 따옴표 처리)
    Client([User Client]):::client
    Web["Web API Server"]:::server
    DS["Dedicated Server"]:::server
    Redis[("Redis")]:::storage
    DB[("Database")]:::storage

    %% Flow
    Client -- "1. Auth & Request API" --> Web
    Client -- "2. Connect & Play" --> DS

    %% Backend Connection
    Web <-- "Data Sync" --> Redis
    DS <-- "Data Sync" --> Redis
    
    Web <-- "Read / Write" --> DB

    %% Subgraph for Logical Grouping
    subgraph Backend [Backend Infrastructure]
        direction TB
        Web
        DS
        Redis
        DB
    end
    %% Subgraph 스타일 적용
    class Backend subgraph_style
