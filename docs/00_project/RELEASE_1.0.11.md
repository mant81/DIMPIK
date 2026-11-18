# DIMPIK 1.0.11 검증 기록

검증일: 2026-11-18

## 변경 사항

- DPI 120% 환경에서 실행 중인 v1.0.10 편집기 창 전체를 PNG로 캡처해 `docs/02_screen/screenshots/dimpik-v1.0.10-actual-2026-11-18.png`에 저장했습니다. 이전에 잘려 보이던 화면 캡처를 교체했습니다.
- 앱, 네이티브 런처 및 설치 프로그램의 버전을 1.0.11로 맞췄습니다. 편집 기능 코드는 변경하지 않았습니다.

## 검증 결과

- `build-installer.ps1`: 통과. 네이티브 런처, 편집기와 설치 프로그램 생성.
- `dotnet run --project tests/DIMPIK.Smoke/DIMPIK.Smoke.csproj -c Release`: 전체 스모크 통과.
- 실제 화면 PNG: 1746×1013픽셀, 41,780 bytes. 창 전체와 중앙 정렬을 육안 확인했습니다.
- 설치·제거와 상주 메모리는 이번 버전에서 다시 검사하지 않았습니다. v1.0.10 검증 결과는 `RELEASE_1.0.10.md`에 있습니다.

## 설치 파일

- 경로: `release/setup/dimpik_inst_x64_1.0.11.exe`
- 크기: 36,788,781 bytes
- 제품 버전: 1.0.11
- 게시자: AAStudio.kr
- 서명: 없음
- SHA-256: `937d60e9f6bb8c47b03489f113100dd7014e36387d165dad17dc44033a932ea6`
