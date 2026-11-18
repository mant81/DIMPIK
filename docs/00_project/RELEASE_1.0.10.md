# DIMPIK 1.0.10 검증 기록

검증일: 2026-11-18

## 변경 사항

- 캡처 이미지 hover 선택 시 도형과 같은 테두리 및 8개 크기 조절 핸들을 표시합니다. 핸들 직접 드래그와 이미지 선택 해제를 지원합니다.
- 파일 메뉴에 `캡처 전체 닫기`와 `끝내기`를 추가하고 트레이 메뉴 및 도움말의 종료 표현을 통일했습니다.
- 픽셀 효과 적용/미리보기 영역을 원본 이미지가 아닌 캔버스 크기에 맞춰 자릅니다.
- 네이티브 런처 및 설치 프로그램 버전 정보를 1.0.10으로 맞췄습니다.

## 검증 결과

- `dotnet build src/DIMPIK/DIMPIK.csproj -c Release`: 통과, 경고 0·오류 0.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 전체 스모크 통과.
- `tools/Test-NativeSecurity.ps1`: 통과, 긴 데이터 경로 4종 안전 거부.
- `tools/Test-SessionLifecycle.ps1`: 통과, 암호화 세션 2회 복원 후 트레이 종료 시 제거.
- `tools/Test-Installer.ps1`: 통과, 테스트 설치 2회·자동 실행 등록 확인·제거 및 등록 정리.
- `tools/Test-Resident.ps1`: 실행 중인 DIMPIK 트레이와 충돌해 시작하지 못했습니다. 기존 트레이 프로세스는 종료 권한이 거부되어 메모리 예산 결과를 확인하지 못했습니다.
- 실제 다중 모니터 DPI 전환은 수동 검증하지 않았습니다.

## 설치 파일

- 경로: `release/setup/dimpik_inst_x64_1.0.10.exe`
- 크기: 36,787,904 bytes
- 제품 버전: 1.0.10
- 게시자: AAStudio.kr
- 서명: 없음
- SHA-256: `5f18c6bab525601b5aa33ad27a0719aa21c983e2f657203191e6d724256237a1`
