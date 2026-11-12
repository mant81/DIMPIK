# DIMPIK 1.0.6 검증 기록

검증일: 2026-11-12 (Asia/Seoul). Windows, .NET SDK 10.0.403, Windows Forms PerMonitorV2.

## 변경 사항

- 로그인 자동 실행 체크를 즉시 적용하고 앱 설정 저장 결과와 분리했습니다.
- 업데이트 안내에 홈페이지 링크와 문의 이메일을 표시하고, 링크는 기본 브라우저에서 엽니다.
- 모자이크 기본 블록 크기를 24px로 변경하고 모자이크·블러 속성 설명을 구분했습니다.
- 트레이 새 시작 시 이전 세션을 폐기하며, 같은 트레이 프로세스에서 편집기를 다시 열면 저장 세션을 복원합니다.
- 업데이트 설치에서 기존 설치 경로를 유지하고 실행 중인 DIMPIK을 닫도록 설정했습니다.
- 문서와 실제 UI 검증 자료를 정리했습니다.

## 통과

- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 기능·옵션·자동 실행·레이아웃 스모크 전체 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-NativeSecurity.ps1`: 긴 경로 4종 안전 거부 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Resident.ps1`: 프로세스 1개 유지, 메모리 제한 통과. 세 주기 작업 집합 8.24~8.41MiB, 전용 커밋 1.47MiB. 편집기 재실행 2회와 세션 복원 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-SessionLifecycle.ps1`: 두 차례 세션 복원과 트레이 종료 시 세션 삭제 통과.
- `powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1`: 네이티브 런처, 편집기, 설치 패키지 빌드 성공.
- 설치 패키지와 런처·편집기 EXE/DLL의 제품 버전 1.0.6, 제작사 AAStudio.kr 확인.

## 제한 및 미완료

- `tools/Test-Installer.ps1` 설치 검사는 실행됐지만 관리 환경이 시작 메뉴 디렉터리 및 HKCU 제거 레지스트리 키 생성을 오류 5(액세스 거부)로 차단해 설치 프로그램이 롤백했습니다. 설치·제거 반복 결과는 통과로 간주하지 않습니다.
- `tools/Test-LicenseUI.ps1`은 PowerShell 파서 오류로 실행되지 않았습니다.
- 실제 로그인 후 자동 실행, 다중 모니터 DPI 전환, 물리 마우스 창 이동/크기 조절 및 관리자 권한의 Program Files 설치는 미검증입니다.
- 공인 코드 서명과 백신 검사는 미적용입니다.

## 배포 파일

- `release/setup/dimpik_inst_x64_1.0.6.exe` (36,792,591 bytes)
- SHA-256: `203e982ef20ade94af06434d4f27314c883fcab80636baf1887062322133190e`
- 확인 화면: [`dimpik-release-ui.png`](../02_screen/screenshots/dimpik-release-ui.png)
