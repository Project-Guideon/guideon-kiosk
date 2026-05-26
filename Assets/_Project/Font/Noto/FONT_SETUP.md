# Noto 폰트 설정 가이드 — Step 8 UI Toolkit

## 1. 폰트 다운로드

아래 Google Fonts 링크에서 각 폰트를 다운로드하세요.

**Noto Serif KR**
- 다운로드: https://fonts.google.com/noto/specimen/Noto+Serif+KR
- 필요 Weight: Regular(400), Medium(500), SemiBold(600), Bold(700)
- 파일명: `NotoSerifKR-Regular.otf`, `NotoSerifKR-Medium.otf`, `NotoSerifKR-SemiBold.otf`, `NotoSerifKR-Bold.otf`

**Noto Sans KR**
- 다운로드: https://fonts.google.com/noto/specimen/Noto+Sans+KR
- 필요 Weight: Light(300), Regular(400), Medium(500), Bold(700)
- 파일명: `NotoSansKR-Light.otf`, `NotoSansKR-Regular.otf`, `NotoSansKR-Medium.otf`, `NotoSansKR-Bold.otf`

다운로드한 `.otf` 파일을 **이 폴더** (`Assets/_Project/Font/Noto/`)에 복사하세요.

---

## 2. TMP SDF Atlas 생성

Unity Editor 메뉴에서 실행:

```
Tools > GUIDEON > Generate TMP SDF (Noto)
```

이 메뉴가 생성하는 SDF Assets:
- `NotoSerifKR-Bold SDF.asset` → 제목/강조용
- `NotoSerifKR-Medium SDF.asset` → 화면 제목용
- `NotoSansKR-Regular SDF.asset` → 본문용
- `NotoSansKR-Light SDF.asset` → 보조 텍스트용

저장 위치: `Assets/_Project/Font/Noto/SDF/`

> **설정**: Atlas Resolution 4096×4096, Static, 한글 상용 2350자(KS X 1001)

---

## 3. 완료 확인

- [ ] `.otf` 파일 8개가 이 폴더에 있음
- [ ] `SDF/` 폴더에 `.asset` 4개가 생성됨
- [ ] Unity Console에 에러 없음
