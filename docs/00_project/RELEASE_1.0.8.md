# DIMPIK 1.0.8 검증 기록

검증일: 2026-11-17 (Asia/Seoul). Windows, .NET SDK 10.0.403, Visual Studio 2022 Build Tools, Inno Setup 6.7.4.

## 변경 사항

- 모니터 해상도·배율 전환 뒤 캔버스, 제목 표시줄 버튼, 하단 상태 표시줄을 다시 배치합니다. 표시 영역에 들어가는 캔버스는 중앙에 배치하고, 큰 캔버스는 스크롤 위치를 유지합니다.
- DPI 전환 후 최소화·최대화·닫기 버튼이 제목 표시줄 안에 다시 배치되도록 합니다.
- 밝은 배경에서 창 경계를 구분하도록 얇은 외곽선과 Windows 기본 그림자를 적용합니다.
- 제목 표시줄 DPI 처리의 컴파일 오류를 수정했습니다.

## 통과

- `dotnet build src/DIMPIK/DIMPIK.csproj -c Release`: 경고 0, 오류 0.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 기능 스모크 통과. 현재 디스플레이 DPI 120에서 탭·붙여넣기·Undo·캔버스 폭 검사를 통과했습니다.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release -- --contextual-toolbar-test`: 창 크기 변경, 최대화·복원, 메뉴·상태 표시줄 배치 통과.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release -- --security-test`: DPAPI, 세션 무결성, 데이터 제한 검사 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-NativeSecurity.ps1`: 비정상 경로 4종 안전 거부 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-SessionLifecycle.ps1`: 암호화 세션 반복 복원과 트레이 종료 시 삭제 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1 -Compiler "<Inno Setup 6.7.4 ISCC.exe>"`: 네이티브 런처, 편집기, 설치 파일 빌드 성공.

## 제한 및 실패

- `tools/Test-Resident.ps1`은 두 번 실행했고, 두 번 모두 대기 작업 집합 10,076,160 bytes로 10,000,000 bytes 상주 메모리 기준을 넘었습니다. 상주 메모리 기준 통과로 표시하지 않습니다.
- 실제 여러 모니터 간 이동과 100/125/150/200% DPI 전환, 설치·제거 UI는 수동 검증하지 않았습니다.
- 설치 파일은 코드 서명되지 않았습니다. Windows SmartScreen 경고가 나타날 수 있습니다.

## 배포 파일

- `release/setup/dimpik_inst_x64_1.0.8.exe` (36,784,396 bytes)
- SHA-256: `9f159e5ba738e8cdda5c62c47d58ed850ca846db4cdaad87f06fb5e7cca44cff`
- 설치 파일 제품 버전: 1.0.8
