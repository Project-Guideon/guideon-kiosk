# GUIDEON Kiosk

관광지에 설치되는 AI 안내 키오스크의 Unity 클라이언트입니다.
3D 마스코트 캐릭터가 음성 대화로 관광객을 안내합니다.

---

## 기술 스택

- **엔진**: Unity 6 LTS (URP 3D)
- **언어**: C# 9.0
- **3D 캐릭터**: UniVRM + uLipSync
- **비동기**: UniTask
- **통신**: NativeWebSocket, UnityWebRequest
- **인증**: Device Token (Kiosk BFF)

## 프로젝트 열기

1. [Unity Hub](https://unity.com/download) 설치
2. Unity 6 LTS 설치 (Windows Build Support IL2CPP 모듈 포함)
3. 이 저장소 클론
4. Unity Hub → Open → 프로젝트 폴더 선택
5. `Assets/StreamingAssets/config.json`에 서버 URL 및 Device Token 입력

## 설정 파일

```json
// Assets/StreamingAssets/config.json
{
  "server": {
    "baseUrl": "https://kiosk-api.guideon.com/api/v1",
    "wsUrl": "wss://kiosk-api.guideon.com/ws/v1"
  },
  "device": {
    "token": "DEVICE_TOKEN_HERE"
  }
}
```

로컬 환경은 `config.local.json`을 사용합니다 (gitignore 적용됨).

