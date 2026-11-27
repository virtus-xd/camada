# Güvenlik

## Desteklenen sürümler

Güvenlik düzeltmeleri en son yayımlanan sürüme ve `main` dalına yapılır.

## Güvenlik açığı bildirmek

Güvenlik açıklarını **herkese açık issue olarak açmayın.** GitHub'ın özel bildirim formunu kullanın:

**[Güvenlik açığı bildir](https://github.com/virtus-xd/camada/security/advisories/new)**

Bildirimde açığın ne olduğunu, nasıl yeniden üretileceğini ve etkisini yazın. İlk yanıtı 7 gün içinde vermeyi hedefliyoruz; düzeltme yayımlanana kadar ayrıntılar gizli tutulur.

## Kapsam örnekleri

- Görev çubuğunun geri getirilemediği, kullanıcıyı görev çubuğusuz bıraktığı durumlar.
- Gemini API anahtarının, Ada hafızasının ya da Spotify belirtecinin sızması (bunlar bilgisayarda Windows DPAPI ile şifreli tutulur).
- Mikrofon sesinin Ada kapalıyken ya da kullanıcının izni olmadan gönderilmesi.
- Camada'nın başka bir uygulamaya ya da kullanıcıya yetkisiz erişim vermesi.

---

## In English

Security fixes target the latest release and the `main` branch. **Do not open public issues for vulnerabilities**; use GitHub's private reporting form: **[Report a vulnerability](https://github.com/virtus-xd/camada/security/advisories/new)**. Describe the issue, how to reproduce it and its impact. We aim to respond within 7 days and keep details private until a fix ships. In scope: the taskbar failing to restore, leaks of the Gemini API key, Ada's memory or the Spotify token (stored encrypted with Windows DPAPI), microphone audio sent while Ada is off or without consent, and any unauthorized access granted by Camada.
