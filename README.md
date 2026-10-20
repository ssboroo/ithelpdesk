# RAINY Remote Native 3

**RustDesk / AnyDesk / Python ашиглахгүй**, өөрийн Windows native алсын дэлгэц, гар/хулганы удирдлагын эхний хувилбар. Клиент C# / .NET 8 WinForms, relay TypeScript / Bun WebSocket. Үйлчилгээ болон дэлгэцийн сесс өөрийн RAINY Remote цонхонд ажиллана.

[Installer болон portable татах](https://github.com/ssboroo/ithelpdesk/releases/latest)

## Хэрэглэх

1. Хоёр Windows x64 компьютерт шинэ `RainyRemote-Portable.exe` нээнэ (эсвэл Setup суулгана). .NET runtime тусад нь суулгахгүй.
2. Программ өөрийн relay серверт автоматаар холбогдож 9 оронтой ID, 6 оронтой код авна. Интернэт шаардлагатай.
3. Тусламж авах тал ID, кодоо техникчдээ өгнө. Техникч оруулаад «Холбогдох» дарна.
4. Хүлээн авах тал «Зөвшөөрөх» сонгоно. Гар/хулгана удирдуулах бол тусдаа checkbox тэмдэглэнэ; default нь зөвхөн харах.
5. **Хоёр талд гарсан 8 тэмдэгт баталгаажуулах код ижил эсэхийг утсаар харьцуулна.** Харах тал «Код таарлаа», хуваалцах тал «Код таарлаа — Хуваалцах» дарсны дараа л дүрс дамжина. Код зөрвөл салгана.
6. Хуваалцаж буй компьютерийн дээр улаан «САЛГАХ» самбар байнга харагдана. Аль ч тал салгахад дүрс болон input зогсоно. Программ хаагдахад мөн холболт сална.

## Одоогоор ажиллах хүрээ

- Primary monitor capture, 1280×720 хүртэл, JPEG quality 45, ойролцоогоор 6 fps (сүлжээ/CPU-ээс хамаарна).
- Өөрийн viewer цонх, хэмжээ тааруулалт, захын хар зайг тооцсон хулганы координат.
- Зөвшөөрсөн үед гар, зүүн/баруун/дунд click, wheel.
- Read-only mode, зөвшөөрөх/татгалзах, хүсэлт цуцлах, session disconnect.
- Хаяг, код нь холболт бүрт шинээр үүснэ. Байнгын нууц үг, unattended service, autostart байхгүй.

**Файл дамжуулах, clipboard, дуу, олон дэлгэц, UAC / secure desktop / Ctrl+Alt+Del, hardware video codec, address book, auto-reconnect хараахан хийгдээгүй.** Эдгээрийн хуучин RustDesk-д тулгуурласан товч болон боломжийг шинэ native хувилбарт ажилладаг мэт харуулаагүй. AnyDesk-тэй дүйцэх latency / үйлдвэрлэлийн түвшний гүйцэтгэл, аюулгүй байдлын аудит хараахан баталгаажаагүй.

## Холболт ба криптографи

- Relay: `wss://rainy-remote-relay-production.up.railway.app/ws`, Railway RAINY REMOTE project.
- TLS дээр peer хооронд ephemeral ECDH P-256 → SHA256 key → AES-256-GCM payload.
- Session ID, packet sequence нь authenticated additional data-д багтана. Replay болон session солихыг шалгана.
- ECDH public key-ийг relay дамжуулдаг тул **хоёр талын баталгаажуулах кодыг бие даасан сувгаар харьцуулах шаардлагатай**. Кодыг шалгалгүй баталбал malicious relay / key substitution-аас хамгаалах баталгаа байхгүй.
- Relay нь зөвшөөрөгдсөн хоёр socket-ийг л холбоно; frame/input ciphertext-ийг санах ойгоор дамжуулна. Дэлгэц, PIN, session content дискэнд болон application log-д хадгалахгүй.
- PIN шалгалт, host consent, 45 секунд хүсэлтийн timeout, IP/target attempt limit, payload/rate/backpressure limits бий.
- Relay нэг replica, ephemeral state; restart хийхэд бүх сесс сална. Хэмжээг 200 socket хүртэл хязгаарласан, autoscaling / HA / account controls хараахан хийгдээгүй.

Энэ нь custom protocol-ийн **эхний хувилбар**. Өөрийн зөвшөөрөлтэй төхөөрөмжүүд дээр туршина; audit болон олон сүлжээн дээрх бодит туршилтгүйгээр байгууллагын үндсэн remote access болгон зарлахгүй.

## Шалгалт

CI дараахыг гүйцэтгэнэ:

- Live relay дээр PIN алдаа, татгалзах, хослол, гаднын socket хориглох, packet forwarding, replay, disconnect.
- Native ECDH / AES-GCM roundtrip, replay, session isolation.
- Windows native capture болон өөрийн viewer JPEG rendering.
- **Хоёр native transport клиент → live relay → шифрлэсэн Windows screenshot → decrypt/compare**, encrypted input packet, disconnect.
- Windows install / startup / uninstall болон portable startup.

Нэг Windows runner дээрх тест нь хоёр тусдаа физик Windows төхөөрөмжийн keyboard/mouse үйлдэл, хэрэглэгчийн consent flow, удаан хугацааны reliability-г бүрэн орлохгүй. Тэдгээр нь нэмж бодитоор шалгах ажил хэвээр.

## Бүтээх

Windows x64 + .NET 8 SDK + Inno Setup 6:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-windows.ps1 -Version 3.0.0
```

`native/Client/BuildConfig.cs` нь анхдагч relay хаяг. Клиент дээрх серверийн хаягийг өөрчилж болно; WSS заавал, зөвхөн loopback дээр WS зөвшөөрнө. Локал серверийн тохиргоо `%LOCALAPPDATA%\RainyRemote3\server.txt`; ID/PIN/session key хадгалахгүй.

`relay/index.ts` нь deployed Railway Function-ийн эх код. Шинэчлэлт хийхдээ function-ийн source-г энэ файлаар шинэчилж, relay tests ажиллуулна. Repository push нь клиент installer-ийг автоматаар бүтээнэ; relay source update нь тусдаа deployment үйлдэл.

RustDesk суулгасан байсан ч энэ программ түүнийг нээхгүй, устгахгүй, тохиргоонд нь хандахгүй. Өмнөх Helpdesk өгөгдлийг уншихгүй.
