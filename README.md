# Head Smash — 플레이 가능한 프로토타입

상대의 머리를 박치기로 부수고, 흩어진 경험치를 먹으며 내 머리를 키우는 3D 아레나 게임입니다. slither.io처럼 계속 전진하며 방향을 조절합니다. 모바일 가로 화면과 두 손 조작을 기준으로 만들었습니다.

## 실행

- **Android:** `Builds/Android/HeadSmash.apk`를 휴대폰으로 복사해 설치 → **LET'S SMASH!**. Android 8.0 이상, ARM64 기기용 테스트 서명 APK입니다.
- **Windows:** `Builds/HeadSmash/HeadSmash.exe` 실행 → **LET'S SMASH!**
- **Unity:** `Assets/HeadSmash/Scenes/HeadSmashArena.unity` 열기 → ▶ Play → **LET'S SMASH!**
- 실행 파일을 다른 곳으로 옮길 때는 `Builds/HeadSmash` 폴더 전체를 함께 옮깁니다.
- Unity 버전: **6000.4.2f1**, URP 17.4, 새 Input System, uGUI + TextMeshPro.

## 조작

| 기능 | PC | 터치 화면 |
|---|---|---|
| 방향 조절 | WASD / 방향키 | 왼쪽 아래 조이스틱 드래그 |
| 박치기 | Space / 마우스 오른쪽 버튼 | 오른쪽 아래 SMASH 버튼 |
| 일시정지 / 계속 | Esc / 상단 II 버튼 | 상단 II 버튼 |
| 시작 / 부활 / 결과 후 새 판 | Enter / 화면 버튼 | 화면 버튼 |
| 효과음 켜기 / 끄기 | 상단 SFX 버튼 | 상단 SFX 버튼 |

입력이 없어도 현재 방향으로 계속 전진합니다. 조이스틱은 마우스로도 드래그할 수 있습니다. 가장자리에 닿으면 안쪽으로 방향을 틀어주세요.

## 한 판의 흐름

1. 12명의 AI와 3분 동안 경쟁합니다. 우측 상단은 머리 경험치 순위, 아래는 미니맵입니다.
2. 작은 경험치 구슬을 먹으면 점수가 오르고 머리가 커집니다. 구슬은 체력도 조금 회복합니다.
3. 박치기는 약 0.3초간 돌진하며 기본 재사용 대기시간은 1.65초입니다. 일반 접촉은 밀어내기만 합니다.
4. 머리가 클수록 피해와 충돌 범위가 커집니다. 뒤에서 맞히면 피해가 35% 증가하므로 작은 머리도 기습할 수 있습니다.
5. 체력이 떨어진 머리에는 금이 생깁니다. 체력 0이면 머리가 깨지고 경험치가 흩어집니다. 처치자에게 즉시 15 XP도 지급됩니다.
6. 피해를 받지 않고 6초가 지나면 서서히 회복합니다. AI는 경험치를 찾거나 상대를 추격하고, 위험하면 도망갑니다.
7. AI는 처치 후 다시 나타납니다. 플레이어는 **BOUNCE BACK**으로 경험치 55%를 보존하고 돌아옵니다. 부활 시 3초 보호막이 있으며 공격하면 즉시 해제됩니다.
8. 제한 시간이 끝나면 최종 순위와 최고 점수를 보여줍니다. **ONE MORE ROUND**로 다시 시작합니다.

일시정지와 플레이어 부활 선택 화면에서는 아레나 전체와 제한 시간이 멈춥니다. 왕관은 현재 최고 경험치 캐릭터에게 표시됩니다.

## 수정하기

UI는 씬의 **HeadSmashUI** Canvas와 `Assets/HeadSmash/UI/Prefabs/HeadSmashUI.prefab`에서 직접 수정합니다. Inspector 위쪽의 HUD / Intro / Pause / Knockout / Results 버튼으로 각 화면을 미리볼 수 있습니다. [UI 편집 안내](Assets/HeadSmash/UI/README.md)에 Hierarchy와 수정 위치를 정리했습니다.

씬의 **Head Smash / Game** 오브젝트에 있는 `HeadSmashGame` Inspector에서 상대 수, 라운드 시간, 구슬 수, 이동 속도, 돌진 속도, 쿨다운, 피해량을 조절할 수 있습니다. `arenaRadius`를 바꿀 때는 저장된 바닥과 범퍼 크기도 함께 맞춰주세요.

| 파일 | 역할 |
|---|---|
| `Assets/HeadSmash/Scripts/HeadSmashGame.cs` | 라운드 상태, 아레나 시뮬레이션, 충돌·처치·경험치, 카메라, 효과음 |
| `Assets/HeadSmash/Scripts/SmashActor.cs` | 캐릭터 생성, 성장, 조작, AI, 돌진, 애니메이션 |
| `Assets/HeadSmash/Scripts/HeadSmashHUD.cs` | 저장된 UI에 게임 값과 화면 상태를 연결하는 컨트롤러 |
| `Assets/HeadSmash/UI/Prefabs/HeadSmashUI.prefab` | Inspector에서 직접 수정하는 Canvas와 네 화면 |
| `Assets/HeadSmash/UI/Prefabs/HitText.prefab` | 타격 글자 모양과 서체 |
| `Assets/HeadSmash/Scripts/SmashJoystick.cs` | 포인터별 조이스틱과 박치기 버튼 입력 |
| `Assets/HeadSmash/Scripts/SmashFx.cs` | 타격 파편, 돌진 먼지와 충격파 |
| `Assets/HeadSmash/Editor/HeadSmashBuilder.cs` | 아레나 생성과 Windows 빌드 메뉴 |
| `Assets/HeadSmash/Editor/HeadSmashValidation.cs` | 실제 런타임 로직을 이용한 18개 통합 검증 |

**Head Smash → Create or rebuild prototype arena**는 생성된 프로토타입 씬을 다시 만듭니다. 수동으로 배치한 내용을 유지하려면 먼저 씬을 복사하세요. **Head Smash → Build Windows prototype**으로 실행 파일을 다시 만들 수 있습니다.

Android APK를 다시 빌드하려면 Build Profiles에서 Android로 전환한 다음 **Head Smash → Build Android APK**를 실행합니다. 앱 식별자는 `com.headsmash.prototype`, 버전은 `0.1.0`이며 IL2CPP / ARM64 / OpenGL ES 3로 빌드합니다. 직접 설치용 테스트 서명을 사용합니다.

## 검증과 범위

- Unity 컴파일 오류 0개. Play 모드 실제 프레임 진행, 시작 버튼, 키보드 방향 전환을 확인했습니다.
- 이동, 성장·회복, 돌진 쿨다운과 마지막 구간 판정, 상대를 완전히 통과하는 고속 충돌, 중복 피해 방지, 처치·경험치 드롭, AI 부활, 구슬 흡수, 경계, 플레이어 부활, 일시정지, 시간 종료, 새 판 초기화를 포함한 **18개 통합 검사 통과**. 결과: `Documentation/Validation.txt`.
- Play 모드에서 **Head Smash → Validate gameplay (Play mode)**를 실행하면 다시 확인할 수 있습니다. 검증 후 새 판의 일시정지 화면으로 돌아갑니다.
- **온라인 멀티플레이는 아직 없습니다.** 상대와 순위는 로컬 AI를 기준으로 계산합니다.
- 터치 조작과 Safe Area 대응을 구현했습니다. Android 설치용 APK를 제공하며 실기기에서의 플레이 검증과 iOS 빌드는 진행하지 않았습니다.
- 캐릭터와 이펙트는 코드로 만든 임시 3D 아트입니다. 효과음도 코드로 생성하며 HUD 문구는 영어입니다.
- 기존 `SampleScene`과 사용자의 기획 파일은 그대로 보존했습니다. 기본 실행 씬은 새 `HeadSmashArena`로 설정했습니다.
