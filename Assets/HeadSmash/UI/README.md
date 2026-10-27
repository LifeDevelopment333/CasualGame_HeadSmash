# Unity에서 UI 수정하기

UI는 씬에 배치된 **uGUI Canvas 프리팹**입니다. Play하지 않아도 Hierarchy에서 선택하고 수정할 수 있습니다.

## 열기

- 씬: `Assets/HeadSmash/Scenes/HeadSmashArena.unity`
- 전체 UI 프리팹: `Assets/HeadSmash/UI/Prefabs/HeadSmashUI.prefab`
- 타격 글자 프리팹: `Assets/HeadSmash/UI/Prefabs/HitText.prefab`
- 모양 스프라이트: `Assets/HeadSmash/UI/Sprites/`

씬의 **HeadSmashUI**를 선택하면 Inspector 위쪽에 **HUD / Intro / Pause / Knockout / Results** 미리보기 버튼이 나타납니다. 편집 모드에서 원하는 화면을 켜고 하위 오브젝트를 수정하세요. 실행하면 게임 상태에 따라 저장된 화면을 표시합니다.

## Hierarchy

```text
HeadSmashUI  [Canvas, CanvasScaler, GraphicRaycaster, HeadSmashHUD]
├─ SafeArea
│  ├─ HUD                 점수, 체력, 시계, 순위, 미니맵, 알림
│  ├─ Controls            왼쪽 조이스틱, 오른쪽 박치기 버튼
│  ├─ HitFeedbackLayer    타격 글자가 잠시 표시되는 영역
│  └─ Screens
│     ├─ IntroScreen
│     ├─ PauseScreen
│     ├─ KnockoutScreen
│     └─ ResultsScreen
└─ Input / EventSystem    새 Input System의 UI 입력
```

## 수정할 컴포넌트

| 바꾸려는 항목 | 수정 위치 |
|---|---|
| 위치, 크기, 앵커 | 해당 오브젝트의 **RectTransform** |
| 패널·버튼 색상, 이미지 | **Image → Color / Source Image** |
| 제목, 설명, 버튼 문구, 폰트 | 하위 **TextMeshPro - Text (UI)** |
| 버튼 동작 | **Button → On Click()**. `HeadSmashHUD`의 기능이 미리 연결되어 있음 |
| 박치기 버튼 | **Smash Press**. Button의 On Click 이벤트를 누르는 순간 한 번 실행 |
| 조이스틱 모양 | `Controls/Movement joystick`과 그 아래 `Inner ring`, `Stick` |
| 미니맵 표식 모양 | `HUD/ArenaRadar/Markers/PlayerMarker`, `OpponentMarkerTemplate` |
| 타격 글자 크기·폰트·그림자 | `HitText.prefab` |

화면 레이아웃, 정적 문구와 버튼 이벤트는 런타임에 다시 만들지 않습니다. 프리팹을 더블클릭하여 수정하면 재사용되는 원본이 바뀝니다. 씬에서 수정하면 해당 씬의 Prefab Override로 저장할 수 있습니다. **Play 중 수정한 내용은 Play 종료 시 되돌아가므로 편집 모드에서 수정하고 저장하세요.**

## 게임이 갱신하는 값

점수·체력·시간·순위·쿨다운·경험치 바는 게임 값에 맞춰 갱신됩니다. 이들의 서체, 크기와 위치는 일반 컴포넌트에서 바꾸고, 동적 문구 형식은 **HeadSmashUI → HeadSmashHUD → Dynamic Text Formats**에서 바꿉니다.

- Level Format: `{0}` = 레벨, `{1}` = 다음 레벨까지 필요한 XP
- Health Format: `{0}` = 체력
- Timer Format: `{0}` = 분, `{1}` = 초
- Rank Format: `{0}` = 내 순위, `{1}` = 내 점수
- Attacker Format: `{0}` = 처치한 상대 이름
- Knockout Format: `{0}` = 점수, `{1}` = 처치 수, `{2}` = 순위
- Results Format: `{0}` = 순위, `{1}` = 점수, `{2}` = 처치 수, `{3}` = 최고 점수

체력·시간의 일반 색상은 Image/TMP에서 읽고, 위험 상태 색상은 **Warning Colors**에서 지정합니다. 기본 하단 안내는 `HUD/Hint`의 텍스트에서 읽습니다. 레벨업·타격 알림의 내용은 게임 코드가 전달합니다.

미니맵의 상대 표식과 타격 글자만 저장된 템플릿/프리팹을 복제합니다. 나머지 HUD와 네 화면은 씬에 저장된 오브젝트를 재사용합니다.

한글 문구를 쓰려면 한글 글리프를 포함한 TMP Font Asset을 해당 텍스트에 지정하세요. 현재 기본 폰트는 영문용 Liberation Sans입니다.

## 연결과 검증

`Head Smash / Game`의 **Hud** 필드에는 씬의 HeadSmashUI가 연결되어 있습니다. Canvas나 필수 참조를 삭제했다면 이 연결도 확인하세요. 새 씬에 UI만 추가하려면 **Head Smash → UI → Add editable UI to current scene**을 실행합니다. 기존 UI가 있으면 재생성하지 않고 연결을 유지합니다.

Canvas 참조·버튼 이벤트·텍스트/스프라이트 에셋 검증 결과는 `Documentation/EditableUI-Assets.txt`, Play 모드에서 버튼·조이스틱·화면 전환과 기존 게임플레이를 검증한 결과는 `Documentation/EditableUI-Gameplay.txt`에 기록합니다.
