# DenemeTest - Online Mülakat ve Sınav Sistemi

DenemeTest, C# .NET, ABP Framework, Blazor Server, PostgreSQL ve Docker teknolojileriyle geliştirilmiş online mülakat ve sınav yönetim sistemidir.

Proje; adaylara tek kullanımlık sınav davet linki gönderilmesi, adayın kamera/mikrofon ve tüm ekran paylaşımı izni vererek sınava başlaması, sınav sırasında kamera ve ekran kaydının alınması, proctoring ihlallerinin takip edilmesi ve admin panelde detaylı sınav raporlarının görüntülenmesi üzerine kuruludur.

## Kullanılan Teknolojiler

* C#
* .NET
* ABP Framework
* Blazor Server
* PostgreSQL
* Docker
* Entity Framework Core
* JavaScript
* MediaRecorder API
* Monaco Editor
* Roslyn Code Execution

## Temel Özellikler

### Aday Sınav Akışı

* Adaya tek kullanımlık sınav davet linki gönderilir.
* Aday sınav başlangıç ekranında kamera ve mikrofon izni verir.
* Aday pencere veya sekme değil, zorunlu olarak tüm ekran paylaşımı yapar.
* Aday kayıt ve güvenlik şartlarını kabul etmeden sınava başlayamaz.
* Sınav başladıktan sonra sorular doğrudan yüklenir.
* Sınav boyunca kamera ve ekran kaydı alınır.
* Sınav bitince kayıtlar sunucuya yüklenir.

### Proctoring ve Güvenlik

Sınav sırasında aşağıdaki davranışlar ihlal olarak algılanır:

* Sekme değiştirme
* Alt+Tab / pencere değiştirme
* Odak kaybı
* Sayfa yenileme
* Ekran paylaşımını kapatma
* Geliştirici araçları açma girişimi

İhlal durumunda sınav otomatik olarak iptal edilir. İptal sebebi ve adayın iptal anındaki puanı admin raporuna yansır.

### Kamera ve Ekran Kaydı

Sistem adayın kamera kaydını ve ekran kaydını ayrı `.webm` dosyaları olarak saklar.

Video dosyaları PostgreSQL içine kaydedilmez. PostgreSQL sadece kayıt metadata bilgisini tutar.

Metadata içinde şu bilgiler bulunur:

* ExamSessionId
* Kayıt türü: Kamera / Ekran
* Dosya adı
* Storage path
* MIME type
* Dosya boyutu
* Upload zamanı
* Planlanan silinme zamanı
* Silinme durumu

Bu yapı sayesinde büyük video dosyaları veritabanını şişirmez ve sistem daha ölçeklenebilir hale gelir.

### Admin Panel

Admin panelde aşağıdaki bilgiler görüntülenebilir:

* Aday bilgileri
* Sınav durumu
* Aday puanı
* Aday cevapları
* Proctoring ihlalleri
* Kamera kaydı
* Ekran kaydı
* Kod sorusu test sonuçları
* Kod inceleme sonuçları

Admin bir sınav oturumunu sildiğinde ilgili cevaplar, skorlar, proctoring eventleri, code review kayıtları, recording metadata kayıtları ve fiziksel video dosyaları temizlenir.

### Kodlama Soruları

Kodlama sorularında aday C# kodu yazabilir.

Özellikler:

* Monaco Editor entegrasyonu
* C# kod çalıştırma
* Test case bazlı değerlendirme
* Gizli input / expected output mantığı
* Başarılı / başarısız test sonucu
* Adaya kullanıcı dostu hata mesajı gösterme

Kod çalıştırma tarafında demo güvenliği için aşağıdaki kontroller eklenmiştir:

* Kod uzunluğu limiti
* Input limiti
* Output limiti
* Timeout kontrolü
* `System.IO` engeli
* `System.Net` engeli
* `Process` engeli
* `Reflection` engeli
* `Environment` engeli
* `DllImport` / `unsafe` engeli
* Sonsuz döngü kontrolleri

Production ortamı için kod çalıştırma modülünün Docker sandbox veya ayrı izole servis yapısına taşınması önerilir.

## Proje Mimarisi

Proje ABP katmanlı mimari yapısına uygun olarak geliştirilmiştir.

### Domain Katmanı

Başlıca entity yapıları:

* Test
* Question
* QuestionOption
* CodeTestCase
* Candidate
* ExamInvitation
* ExamSession
* Answer
* Score
* ProctoringEvent
* ExamRecording
* CodeReview

### Application Katmanı

Başlıca servisler:

* TestAppService
* ExamSessionAppService
* ExamRunAppService
* ReportsAppService
* CodeExecutionAppService
* RoslynCodeRunner

### Blazor Katmanı

* Admin test yönetimi
* Admin rapor ekranı
* Admin oturum detay ekranı
* Aday sınav başlangıç ekranı
* Aday sınav runner ekranı

### JavaScript Modülleri

* `exam-media.js`

  * Kamera/mikrofon izinleri
  * Tüm ekran paylaşımı kontrolü

* `recorder.js`

  * Kamera kaydı
  * Ekran kaydı
  * Kayıt upload işlemleri

* `examProctor.js`

  * Sekme değişimi kontrolü
  * Odak kaybı kontrolü
  * Alt+Tab / reload / devtools kısayol kontrolleri

* `codeEditor.js`

  * Monaco Editor entegrasyonu

## Kurulum

### Gereksinimler

* .NET SDK
* Docker Desktop
* PostgreSQL
* pgAdmin
* Visual Studio veya VS Code

### Örnek Connection String

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5436;Database=DenemeTest;Username=appuser;Password=apppass"
  }
}
```

### Migration Çalıştırma

```bash
dotnet build
dotnet run --project src/DenemeTest.DbMigrator
```

### Uygulamayı Çalıştırma

```bash
dotnet run --project src/DenemeTest.Blazor
```

veya Visual Studio üzerinden Blazor projesi başlatılabilir.

## Kayıt Storage Mantığı

Local geliştirme ortamında kayıtlar şu klasörde tutulur:

```text
App_Data/recordings
```

Production ortamı için önerilen storage çözümleri:

* MinIO
* AWS S3
* Cloudflare R2
* Azure Blob Storage

## Production İçin Geliştirme Notları

Bu proje demo ve ürün prototipi seviyesinde çalışır durumdadır. Production ortamı için aşağıdaki geliştirmeler önerilir:

* Recording upload token sistemi
* Chunk-based upload mimarisi
* Object storage entegrasyonu
* Recording retention background worker
* Kod çalıştırma için Docker sandbox
* Daha detaylı rol/yetki kontrolü
* Audit log raporları
* KVKK aydınlatma metni sayfası
* Rate limit / request limit
* Reverse proxy ve HTTPS deployment

## Proje Durumu

Proje şu anda demo, teknik sunum ve iş görüşmesi için gösterilebilir seviyededir.

Tamamlanan ana özellikler:

* Online sınav akışı
* Tek kullanımlık davet linki
* Kamera/mikrofon izni
* Tüm ekran paylaşımı zorunluluğu
* Kamera ve ekran kaydı
* Admin raporları
* Proctoring ihlal takibi
* Kod sorusu çalıştırma
* Recording metadata sistemi
* Video oynatma ve indirme
* Session silme ve video temizleme
