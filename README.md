# Zium

Zium은 Windows 11에서 작업 창 옆에 띄워 쓰는 한 장짜리 할 일 앱입니다. 한 줄씩 적고, 끝나면 같은 자리에서 취소선을 그어요.

## 실행

Windows 11 x64에서 [GitHub 릴리스](https://github.com/sabsalee/zium/releases)의 `Zium-win-x64.zip`을 내려받아 압축을 푼 뒤 `Zium.exe`를 실행하세요. 실행에 필요한 .NET 런타임은 압축 파일에 들어 있습니다.

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

## 개발

Windows에서 .NET 10 SDK로 빌드하고 실행합니다.

```powershell
dotnet run --project src/Zium.Windows/Zium.Windows.csproj
dotnet run --project tests/Zium.Core.Checks/Zium.Core.Checks.csproj
dotnet publish src/Zium.Windows/Zium.Windows.csproj -c Release -r win-x64 --self-contained true
```

동작의 기준과 다음 확장 아이디어는 [설계 문서](DESIGN.md)에 있습니다.
