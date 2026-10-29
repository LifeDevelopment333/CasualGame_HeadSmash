# 캐릭터 애니메이션 편집

`SmashActor.controller`를 Animator 창에서 열고, 개별 `.anim` 파일은 Animation 창에서 편집합니다. 플레이어와 AI 모두 `SmashActor.prefab` 루트의 Animator를 공유합니다. Root Motion은 사용하지 않고 실제 이동과 충돌은 `HeadSmashGame` / `SmashActor`가 처리합니다.

| 레이어 | 상태 / 클립 | 용도 |
|---|---|---|
| Locomotion | Idle, Walk, Dash | 대기, 걷기, 돌진 시 몸의 바운스·발 움직임·머리 기울기 |
| Reactions | Neutral (ReactionNeutral), Hit, Bonk, LevelUp | 피격, 공격 적중, 레벨업 시 squash & stretch. 종료 후 Neutral로 복귀 |
| Status flash | Normal (FlashNormal), Shield (ShieldFlash), Hurt (HitFlash) | 보호막·피격 깜빡임. 피격 표시가 보호막보다 우선 |

| 파라미터 | 형식 | 연결 |
|---|---|---|
| Moving | Bool | 게임 진행 중에는 Walk, 준비 화면에서는 Idle |
| Dashing | Bool | 돌진 시작 / 종료 |
| Hit | Trigger | 피격 |
| Bonk | Trigger | 박치기 적중 |
| LevelUp | Trigger | 플레이어 레벨 상승 |
| Shielded | Bool | 스폰 보호막 |
| Hurt | Bool | 피격 직후 무적 시간 |

애니메이션은 `Squash and stretch`의 높이와 크기, `Left boot` / `Right boot`의 앞뒤 위치, `Head anchor/Head motion`의 위치와 회전을 다룹니다. `Head anchor` 높이와 `Growing head` 크기는 점수에 맞춰 게임 코드가 설정하므로, 해당 두 값은 클립에 키를 추가하지 않습니다. 머리의 크기가 커져도 돌진 포즈와 왕관·균열·얼굴이 함께 따라갑니다.

깜빡임 클립은 `SmashActor.flashAmount`를 0 또는 1로 키잉합니다. 게임 코드는 이 값에 따라 피부 재질만 교체합니다. 이름표·체력바·왕관·균열 표시는 실제 점수와 체력에 연결됩니다.

일시정지·사망·게임 종료 화면에서는 Animator 재생이 멈추며, 부활 시 재생 상태를 초기화합니다. 클립이나 Controller를 수정한 내용은 **Head Smash → Characters → Add Animator to actor prefab** 메뉴를 다시 실행해도 유지됩니다.

Play 모드의 **Head Smash → Validate character animations (Play mode)** 메뉴에서 상태 전환, 실제 포즈, 성장 유지, 깜빡임, 일시정지와 부활을 검증합니다. 검증이 끝나면 새 라운드의 일시정지 화면으로 돌아갑니다.
