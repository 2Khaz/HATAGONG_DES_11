# Phase 1 타일 시각 리소스 적용 보고서

## 적용 구조

- 모든 타일은 공통 `Phase1DamageState`인 `Normal`, `Damage1`, `Damage2`, `Damage3`, `Destroyed`를 유지한다.
- 크기 6종과 파괴 전 상태 4종을 조합해 `Img_[크기]tiles_[01~04]` Block 이미지를 선택한다.
- 등급은 Beige=`Tile_Beige`, Brown=`Tile_Orange`, Gray=`Tile_Gray`, Marble=`Tile_Marble`로 연결한다.
- UI 합성 셰이더가 재질 Texture에 Block 이미지의 명암과 Alpha를 적용한다.
- 기존 HP, 점수, Bag, 등급 가중치 및 파괴 판정 로직은 변경하지 않았다.

## 향후 HP 증가 대응

파괴 상태는 크기나 현재 최대 HP에 종속된 타입이 아니라 모든 `Phase1TileView`가 공통으로 갖는 상태다. 현재 HP가 낮아 일부 상태를 건너뛰는 타일도 동일한 상태 집합과 시각 리소스 매핑을 보유한다. 향후 최대 HP가 증가하면 기존 비율 기반 `CalculateState`가 별도 리소스 변경 없이 네 파괴 전 단계를 사용한다.

## 리소스 및 빌드 안전성

- Block 이미지는 `Resources`에서 Texture2D로 로드한 뒤 Full Rect Sprite로 캐시한다.
- 재질 Texture와 Grade별 합성 Material도 캐시해 타격마다 새 객체를 만들지 않는다.
- 합성 셰이더를 Always Included Shaders에 등록해 Player 빌드의 셰이더 stripping을 방지한다.
- `Tools/HATAGONG/Phase1/Validate Tile Visual Resources`에서 24개 Block, 4개 Texture, 공통 상태 임계값을 검증할 수 있다.
