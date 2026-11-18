# DIMPIK 1.0.9 검증 기록

검증일: 2026-11-18 (Asia/Seoul). Windows 10.0.26300, .NET SDK 10.0.403, Visual Studio Build Tools, Inno Setup 6.7.3.

## 변경 사항

- 캔버스 크기와 원본 캡처 이미지 영역을 분리했습니다. 이미지를 클릭해 선택하면 도형과 같은 여덟 개의 핸들이 나타납니다. 이미지 비율을 제한하지 않고 크기를 바꾸거나 캔버스 안에서 이동할 수 있으며, 캔버스 밖으로 확장한 부분은 합성 결과에서 잘립니다.
- 이미지 테두리와 중립 회색 작업 영역으로 실제 이미지 영역을 구분하고, 격자선은 이미지 바깥 작업 영역에만 그립니다.
- 캔버스·이미지 배치를 실행 취소/다시 실행 및 암호화 세션 저장/복원에 반영했습니다.
- 파일 닫기 경로를 통합하고, 새 설정에서 격자 표시를 기본으로 켭니다.

## 통과

- `dotnet build src/DIMPIK/DIMPIK.csproj -c Release`: 경고 0, 오류 0.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 전체 기능 스모크 통과(캔버스 편집, 자르기, 이미지/주석 복사, 확대, Undo/Redo, 설정 및 DPI 120 포함).
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release -- --contextual-toolbar-test`: 메뉴·창 크기·최대화/복원 UI 검사 통과.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release -- --security-test`: DPAPI, 세션 무결성, 레거시 마이그레이션 및 제한 검사 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-NativeSecurity.ps1`: 비정상 경로 4종 거부 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-SessionLifecycle.ps1`: 암호화 세션 2회 복원 및 트레이 종료 정리 통과.
- `tools/Test-Installer.ps1`: 별도 테스트 설치 프로그램으로 설치 2회, 파일·게시자·시작 등록 검사, 제거와 등록 정리 통과.
- `build-installer.ps1`: 네이티브 런처, 편집기, 1.0.9 설치 프로그램 생성 성공.

## 제한

- `tools/Test-Resident.ps1`: 대기 작업 집합 10,088,448 bytes로 10,000,000 bytes 기준을 초과했습니다. 메모리 예산 통과로 표시하지 않습니다.
- 실제 여러 모니터 간 이동과 100/125/150/200% DPI 전환은 수동 검증하지 않았습니다. 자동 UI 스모크는 DPI 120에서 통과했습니다.
- 서명 인증서는 환경에 설정되어 있지 않아 설치 파일은 서명되지 않았습니다. 공개 다운로드 시 SmartScreen 경고가 나타날 수 있습니다.

## 설치 파일

- 경로: `release/setup/dimpik_inst_x64_1.0.9.exe`
- 크기: 36,797,282 bytes
- 제품 버전 / 게시자: 1.0.9 / AAStudio.kr
- SHA-256: `d20a80b6d40683fa06c1cf43ab063b5d115cfca07229859b8f5030c91cdba3b1`
- Authenticode: NotSigned