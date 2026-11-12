# DIMPIK 1.0.5 검증 기록

검증일: 2026-11-11 (Asia/Seoul). Windows, .NET SDK 10.0.403, Windows Forms PerMonitorV2.

## 통과

- `dotnet build src/DIMPIK/DIMPIK.csproj -c Release`: 경고 0개, 오류 0개.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 전체 기능 스모크 통과. 캡처 탭, 이미지/주석 복사, Undo/Redo, 자르기, 모자이크/블러, 자동 저장, 세션, 옵션, 단축키, 선택 오버레이 및 줄자 포함.
- 별도 스모크 옵션: `--contextual-toolbar-test`, `--security-test`, `--pen-resize-test`, `--number-stamp-test`, `--automatic-region-copy-test`, `--gallery-close-test`, `--icon-test` 모두 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1`: 네이티브 런처, self-contained 편집기와 설치 프로그램 생성 성공.
- `tools/Test-NativeSecurity.ps1`: 긴 경로 4종 안전 거부 통과.
- `tools/Test-Resident.ps1`: 네이티브 단독 트레이 대기, 작업 집합 8.26~8.43MiB / 전용 커밋 1.48MiB. 편집기 2회 열기/닫기와 시작 도중 종료, 암호화 세션 보존 통과.
- `tools/Test-SessionLifecycle.ps1`: 암호화 세션 2회 열기/닫기 및 트레이 종료 시 삭제 통과.
- Inno Setup `/DInstallSmoke`로 별도 테스트 ID와 저장소 내 경로를 사용한 테스트 패키지 생성 후 `tools/Test-Installer.ps1`: 설치 2회, 런처/편집기 분리, 자동 실행 항목, PDB 제외, 제거 및 테스트 자동 실행 항목 정리 통과. 시작 메뉴/테스트 레지스트리 접근을 위해 샌드박스 밖에서 검사함.
- 버전 일치: 네이티브 런처, 편집기 EXE/DLL, 설치 EXE 모두 1.0.5.
- 실행 UI 캡처: `docs/02_screen/screenshots/dimpik-release-ui.png` (확인용 이미지 사용).

## 배포 파일

- `release/setup/dimpik_inst_x64_1.0.5.exe` (36,777,720 bytes)
- SHA-256: `f2714e545841548707d3995aec1e9f68ee35f9aec2388fe508cec0bbaa5bcafa`
- 공인 코드 서명 없음.

## 별도 검증 필요

- 실제 다중 모니터 사이의 DPI 변경, 물리 마우스를 사용한 제목 표시줄 이동/더블클릭과 가장자리 크기 조절.
- 실제 로그아웃/로그인, 다른 관리자 계정 UAC를 통한 설치, 코드 서명 및 백신 검사.
- 이번 설치 검사는 실제 Program Files 설치 대신 별도 테스트 ID와 경로를 사용함.
