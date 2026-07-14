const translations = {
  tr: {
    navFeatures:"Özellikler",navHow:"Nasıl çalışır",downloadWindows:"Windows için indir",heroTitle:"Dosya kopyalama, kontrolünüz altında.",heroBody:"Robocopy’nin gücünü modern bir arayüz, akıllı kuyruklar ve SHA-256 doğrulamasıyla kullanın.",viewGithub:"GitHub’da görüntüle",source:"Kaynak",destination:"Hedef",settings:"Ayarlar",addQueue:"＋ Kuyruğa ekle",queue:"Kuyruk (3)",pauseAll:"Tümünü duraklat",allFiles:"Tüm Dosyalar",media:"Medya",documents:"Belgeler",waiting:"Beklemede",verifyAfter:"Kopyalama sonrası doğrula",skipSuccessful:"Başarılı kopyaları atla",skipExisting:"Hedefteki aynı dosyaları atla",retryErrors:"Hataları yeniden dene",verificationDone:"Doğrulama tamamlandı.",featuresTitle:"Hız için tasarlandı. Güven için doğrulandı.",featureRobocopy:"Robocopy motoru",featureRobocopyBody:"Windows’un yerleşik ve güvenilir kopyalama altyapısı.",featureQueue:"Akıllı kuyruk",featureQueueBody:"Transferleri sırala, duraklat, devam ettir veya yeniden dene.",featureVerify:"İçerik doğrulama",featureVerifyBody:"Boyut kontrolü ya da SHA-256 ile birebir doğrulama.",featureExplorer:"Explorer entegrasyonu",featureExplorerBody:"Sağ tık menüsünden kopyala ve hedefe yapıştır.",workflowTitle:"Üç adımda güvenli transfer",stepOne:"Kaynağı seç",stepOneBody:"Kaynak klasörü ve hedef konumu belirleyin.",stepTwo:"Seçenekleri belirle",stepTwoBody:"Mod, filtre ve doğrulama ayarlarını seçin.",stepThree:"Kopyala ve doğrula",stepThreeBody:"Transferi başlatın; CopyPaste sonucu doğrulasın.",downloadTitle:"CopyPaste’i şimdi deneyin.",downloadBody:"Windows 10 ve 11 için. Kurulum gerektirmeyen taşınabilir paket.",downloadX64:"Windows x64 indir",releaseNotes:"Sürüm notları",trustRobocopy:"Robocopy tabanlı",trustSha:"SHA-256 doğrulama",trustOpen:"Açık kaynak",updateNote:"↻ Güncellemeler GitHub Releases üzerinden güvenle kontrol edilir.",faqTitle:"Sık sorulanlar",faqDelete:"CopyPaste dosya siliyor mu?",faqDeleteAnswer:"Hayır. MIR ve PURGE güvenli varsayılanlarda kapalıdır.",faqInternet:"İnternet bağlantısı gerekiyor mu?",faqInternetAnswer:"Yalnızca güncelleme kontrolü için; kopyalama tamamen yereldir.",faqMenu:"Windows 11 sağ tık menüsü nerede?",faqMenuAnswer:"“Daha fazla seçenek göster” altında.",footerText:"Windows için hızlı ve güvenilir dosya transferi.",releases:"Sürümler",docs:"Belgeler"
  },
  en: {
    navFeatures:"Features",navHow:"How it works",downloadWindows:"Download for Windows",heroTitle:"File copying, under your control.",heroBody:"Use the power of Robocopy through a modern interface, smart queues, and SHA-256 verification.",viewGithub:"View on GitHub",source:"Source",destination:"Destination",settings:"Settings",addQueue:"＋ Add to queue",queue:"Queue (3)",pauseAll:"Pause all",allFiles:"All files",media:"Media",documents:"Documents",waiting:"Waiting",verifyAfter:"Verify after copying",skipSuccessful:"Skip successful copies",skipExisting:"Skip identical destination files",retryErrors:"Retry errors",verificationDone:"Verification complete.",featuresTitle:"Built for speed. Verified for trust.",featureRobocopy:"Robocopy engine",featureRobocopyBody:"Windows’ built-in, dependable copying infrastructure.",featureQueue:"Smart queue",featureQueueBody:"Reorder, pause, resume, or retry your transfers.",featureVerify:"Content verification",featureVerifyBody:"Verify one-to-one with file size checks or SHA-256.",featureExplorer:"Explorer integration",featureExplorerBody:"Copy and paste into a destination from the context menu.",workflowTitle:"Safe transfers in three steps",stepOne:"Choose the source",stepOneBody:"Select the source folder and destination.",stepTwo:"Set your options",stepTwoBody:"Choose mode, filters, and verification settings.",stepThree:"Copy and verify",stepThreeBody:"Start the transfer and let CopyPaste verify the result.",downloadTitle:"Try CopyPaste now.",downloadBody:"For Windows 10 and 11. A portable package with no installation required.",downloadX64:"Download Windows x64",releaseNotes:"Release notes",trustRobocopy:"Powered by Robocopy",trustSha:"SHA-256 verification",trustOpen:"Open source",updateNote:"↻ Updates are securely checked through GitHub Releases.",faqTitle:"Frequently asked questions",faqDelete:"Does CopyPaste delete files?",faqDeleteAnswer:"No. MIR and PURGE are disabled in the safe defaults.",faqInternet:"Is an internet connection required?",faqInternetAnswer:"Only for update checks; copying stays completely local.",faqMenu:"Where is the Windows 11 context menu?",faqMenuAnswer:"Under “Show more options.”",footerText:"Fast and reliable file transfers for Windows.",releases:"Releases",docs:"Documentation"
  }
};

translations.tr.trustOpen = "GitHub Releases";
translations.en.trustOpen = "GitHub Releases";

function setLanguage(language) {
  const selected = translations[language] ? language : "tr";
  document.documentElement.lang = selected;
  document.querySelectorAll("[data-i18n]").forEach(element => {
    const value = translations[selected][element.dataset.i18n];
    if (value) element.textContent = value;
  });
  document.querySelectorAll("[data-language]").forEach(button => {
    button.setAttribute("aria-pressed", String(button.dataset.language === selected));
  });
  document.title = selected === "tr"
    ? "CopyPaste — Güvenilir Windows dosya transferi"
    : "CopyPaste — Reliable Windows file transfers";
  localStorage.setItem("copypaste-language", selected);
}

document.querySelectorAll("[data-language]").forEach(button =>
  button.addEventListener("click", () => setLanguage(button.dataset.language)));

document.querySelector(".footer-language").addEventListener("click", () =>
  setLanguage(document.documentElement.lang === "tr" ? "en" : "tr"));

setLanguage(localStorage.getItem("copypaste-language") || "tr");
