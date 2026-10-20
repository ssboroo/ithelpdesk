# RAINY Remote Native 3

**RustDesk / AnyDesk / Python ашиглахгүй**, өөрийн Windows native алсын дэлгэц, гар/хулганы удирдлагын эхний хувилбар. Клиент C# / .NET 8 WinForms, relay TypeScript / Bun WebSocket. Үйлчилгээ болон дэлгэцийн сесс өөрийн RAINY Remote цонхонд ажиллана.

[Installer болон portable татах](https://github.com/ssboroo/ithelpdesk/releases/latest)

## Хэрэглэх

1. Хоёр Windows x64 компьютерт шинэ `RainyRemote-Portable.exe` нээнэ (эсвэл Setup суулгана). .NET runtime тусад нь суулгахгүй.
2. Программ өөрийн relay серверт автоматаар холбогдож 9 оронтой ID, 6 оронтой код авна. Интернэт шаардлагатай.
3. Тусламж авах тал ID, кодоо техникчдээ өгнө. Техникч оруулаад «Холбогдох» дарна.
4. Хүлээн авах тал «Зөвшөөрөх» сонгоно. Гар/хулгана удирдуулах эсвэл зөвхөн харах гэсэн хоёр тодорхой сонголтоос сонгоно.
5. **Хоёр талд гарсан 16 тэмдэгт баталгаажуулах код ижил эсэхийг утсаар харьцуулна.** Харах тал «Код таарлаа», хуваалцах тал «Код таарлаа — Хуваалцах» дарсны дараа л дүрс дамжина. Код зөрвөл салгана.
6. Хуваалцах төлөв болон «Холболт салгах» товч үндсэн цонхонд харагдана. Аль ч тал салгахад дүрс болон input зогсоно. Программ хаагдахад мөн холболт сална.

## Одоогоор ажиллах хүрээ

- Primary monitor capture, 1920×1080 хүртэл, JPEG quality 78-аас сүлжээнд тааруулж бууруулна, 20 fps хүртэл (сүлжээ/CPU-ээс хамаарна).
- Үндсэн цонхонд багтсан viewer, хэмжээ тааруулалт, захын хар зайг тооцсон хулганы координат.
- Зөвшөөрсөн үед гар, зүүн/баруун/дунд click, wheel.
- Read-only mode, зөвшөөрөх/татгалзах, хүсэлт цуцлах, session disconnect.
- ID нь серверт дахин холбогдоход, PIN нь сесс дуусахад шинээр үүснэ. Байнгын нууц үг, unattended service, autostart байхгүй.

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
- Windows native capture болон өөрийн viewer JPEG rendering, тусгаарласан TextBox дээр keyboard input injection.
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

## Дэлгэц харагдахгүй байсан нөхцөлүүдийн засвар

- Хоёр талын код шалгах үйлдлийг далд жижиг товчоос үндсэн цонхон доторх тодорхой баталгаажуулах самбар руу шилжүүлсэн. Зөвшөөрөл болон кодын шалгалтыг алгасахгүй.
- Viewer нь хүлээлт, баталгаажуулалт, эхний зураг, зураг ирэхгүй удах төлөвүүдийг Монгол тайлбартай харуулна.
- Том кадрыг чимээгүй хаяхыг зогсоож JPEG чанар / хэмжээг төсөвт багтаах adaptive encoding ашиглана.
- Эхний зураг хүрэхэд хүлээн авсан тал баталгааны багц илгээнэ.
- Нэмсэн GUI regression: хоёр жинхэнэ MainForm, host consent, хоёр fingerprint самбар, 3-аас олон үзүүлсэн кадр, салгалт.
- UI зураг нь зөвхөн тусгаарласан CI тестийн орчноос үүсэж, тестийн artifact-д хадгалагдана.

### Ашиглах хугацаа ба гүйцэтгэл
Программд хугацаа дуусах, сарын захиалга, ашиглах хугацааны түгжээ байхгүй. Холболтын серверийн хостинг, сүлжээний зардал тусдаа; серверийг өөрийн хаягаар тохируулж болно. Хөдөлгөөнгүй дэлгэцийн давтан JPEG дамжуулалтыг алгасаж, өөрчлөгдөхөд 20fps хүртэл шинэчилнэ. Интернэт, төхөөрөмж, дэлгэцийн өөрчлөлтөөс бодит хурд хамаарна. Hardware video codec хараахан ашиглахгүй.

## Дамжуулалтын дарааллын шинэчлэлт
- Нэг зураг хүрч харагдсан ACK иртэл дараагийн зургийг барихгүй. Хуучин screenshot-ийн дараалал хуримтлагдахгүй; ACK 12 секунд ирэхгүй бол сессийг дуусгана.
- JPEG decode ажил background thread дээр; UI thread зөвхөн бэлэн bitmap-ийг солино.
- 350ms-аас удаан хариутай үед 720p / quality65 / 140KB төсөв, 700ms-аас удаан үед 540p / quality50 / 65KB төсөв. Сайжирвал 1080p хүртэл автоматаар өснө. Эдгээр нь end-to-end ACK хугацаа бөгөөд ping дангаараа биш.
- MouseMove event бүрийг дараалуулахгүй; 25ms тутамд хамгийн сүүлийн байрлалыг илгээнэ. Click, key-up болон release тусдаа найдвартай дарааллаар дамжина.
- Зөвшөөрөл, код баталгаажуулалт, удирдах эрх, тохиргоо, viewer бүгд нэг үндсэн цонхонд; нэмэлт floating banner байхгүй.
- Relay нэг replica Сингапурт. Байршил ойртсон ч хэрэглэгчийн ISP routing / latency-г Монгол төхөөрөмжөөс хараахан хэмжээгүй.
