# Zium

Zium은 Windows 11에서 작업 창 옆에 띄워 쓰는 한 장짜리 할 일 앱입니다. 한 줄씩 적고, 끝나면 같은 자리에서 취소선을 그어요.

## 실행

Windows 11 x64에서 [GitHub 릴리스](https://github.com/sabsalee/zium/releases)의 `Zium-Setup.exe`를 내려받아 실행하세요. 기존 ZIP 버전이 열려 있으면 트레이에서 먼저 종료합니다. 설치 버전은 같은 로컬 저장 파일에서 할 일과 메모를 이어 불러옵니다.

| 원하는 동작 | 조작 |
| --- | --- |
| 할 일 추가 | 창에 포커스를 둔 뒤 하단에 한 줄을 적고 `Enter` |
| 완료하거나 되돌리기 | 왼쪽 체크박스 클릭 |
| 제목 또는 메모 편집 | 펼친 항목의 **메모 편집**, 우클릭 메뉴 또는 항목에 포커스를 두고 `F2`. 편집 중 다른 제목을 누르면 작성 중이던 내용을 저장한 뒤 새 항목을 편집 |
| 메모 펼치기 | 메모가 있는 줄의 제목 영역이나 오른쪽 꺾쇠 클릭 |
| 순서 바꾸기 | 항목 제목을 잡고 원하는 줄 사이로 끌기 |
| 창 크기 바꾸기 | 창의 테두리 또는 모서리에 표시된 손잡이 끌기 |
| 모서리에 붙이기 | 제목줄을 잡아 놓거나 원하는 모서리 쪽으로 던지기 |
| 책갈피로 접기 | 숨기기 버튼 클릭 또는 가장자리 근처에서 바깥쪽으로 빠르게 던지기 |
| 다시 열기 | 화면 가장자리의 책갈피, 트레이 아이콘 또는 `Ctrl+Alt+Space` |
| 앱 종료 | 트레이 메뉴의 **종료** |

`Ctrl+Alt+Space`는 열기와 접기를 전환합니다. 다른 앱이 이 키를 쓰고 있다면 책갈피와 트레이에서 열 수 있어요. 할 일과 창 상태는 `%LOCALAPPDATA%\Zium\todo.json`에 저장합니다.

## 자동 업데이트

비공개 GitHub 저장소를 사용하므로 설치한 Windows의 Zium 트레이에서 **자동 업데이트 설정**을 열고 다음 항목으로 읽기 토큰을 연결하세요.

1. **GitHub에서 토큰 만들기**를 누르고 Fine-grained 토큰을 만듭니다. Resource owner는 `sabsalee`, Repository access는 **Only select repositories**의 `zium`을 선택합니다.
2. Repository permissions의 **Contents**를 **Read-only**로 설정하고 토큰을 생성합니다. 토큰은 Zium의 입력칸에 붙여넣고 **토큰 연결**을 누릅니다.

토큰은 현재 Windows 사용자 계정으로 암호화해 저장합니다. 앱 시작 시와 실행 중 4시간마다 새 버전을 확인해 백그라운드에서 받습니다. 다운로드된 업데이트는 앱을 종료하거나 다음에 열 때 적용됩니다. 바로 적용할 때는 트레이의 **업데이트하고 다시 시작**을 누릅니다. 토큰이 만료되면 새 토큰으로 연결할 수 있습니다. [GitHub의 토큰 안내](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) · [필요한 릴리스 읽기 권한](https://docs.github.com/en/rest/releases/releases)

## 개발

Windows에서 .NET 10 SDK로 평소처럼 바로 실행합니다. 개발 실행 경로에서는 설치와 업데이트 다운로드가 필요 없습니다.

```powershell
dotnet run --project src/Zium.Windows/Zium.Windows.csproj
dotnet run --project tests/Zium.Core.Checks/Zium.Core.Checks.csproj
```

설치 패키지가 필요할 때는 `dotnet tool restore` 후 `./scripts/package-windows.ps1`을 실행합니다. 앱 버전과 일치하는 `v` 태그를 푸시하면 GitHub Actions가 Windows에서 설치와 업데이트를 확인하고 Setup, 업데이트 피드, 전체 패키지를 릴리스에 올립니다. 하이픈이 들어간 버전은 프리릴리즈로 게시됩니다.

동작의 기준과 다음 확장 아이디어는 [설계 문서](DESIGN.md)에 있습니다.
