# تحديث FramePress إلى 0.1.1

## تحديث الكود

1. أغلق البرنامج. ضع Update-FramePress-v0.1.1.ps1 بجانب FramePress.slnx في مجلد مشروعك، وليس داخل src.
2. افتح PowerShell في مجلد المشروع ونفّذ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Update-FramePress-v0.1.1.ps1
dotnet run --project .\tests\FramePress.Tests\FramePress.Tests.csproj -c Release
dotnet build .\src\FramePress.App\FramePress.App.csproj -c Release
dotnet .\src\FramePress.App\bin\Release\net10.0-windows\FramePress.App.dll --smoke-test
$LASTEXITCODE
```

المطلوب نجاح الاختبارات ونتيجة اختبار الواجهة 0. عند الفشل اقرأ artifacts/ui-smoke-error.txt وartifacts/wpf-bindings.log.
السكربت يأخذ نسخة من ملفات الكود قبل تعديلها، ولا يعدّل الفيديوهات أو vendor أو إعدادات المستخدم. نسخة الكود الاحتياطية ليست نسخة فيديو وتوجد داخل مجلد المشروع فقط.

## اختبار يدوي على نسخ تجريبية

- العربية والإنجليزية: بدّل اللغة والوضع الداكن والفاتح، وافتح إعدادات الدقات. تحقق من تعريب السرعات وخيارات المعالج وكرت الشاشة.
- اضغط «اختبار السرعة والوقت والجودة» واختر فيديو مباشرةً. تحقق من وقت الفيديو ووقت الساعة وحجم متوقع وSSIM أو رسالة تعذر القياس. لا يجب أن تبقى عينة في مجلد الفيديو.
- جرّب مجلدًا فيه فيديوهات ومجلدات داخلية. وضع النسخ يُبقي الأصل وينشئ اسمًا واضحًا مثل clip.framepress-compressed.mp4.
- الاستبدال على نسخة MOV: بعد نجاح التحقق يبقى clip.mp4، ويُحذف clip.mov. لا تبقى ملفات framepress-work أو backup أو transaction جديدة.
- الاستبدال على MP4: يبقى المسار نفسه بمحتوى أصغر. افتحه، وتنقّل للبداية والوسط والنهاية، وافحص الصوت، ثم افتحه في برنامج المونتاج.
- ملف clip.mov مع clip.mp4 موجود بجانبه: يجب أن يتوقف الاستبدال ويحافظ على الملفين.
- فيديو مضغوط مسبقًا: إذا بلغ الناتج حجم الأصل يتوقف الضغط ويُحذف المؤقت ويظل الأصل. لا ترفع خسارة الجودة تلقائيًا للحصول على حجم أصغر.
- إلغاء أثناء الضغط: يبقى الأصل، ويُحذف الناتج المؤقت بعد توقف FFmpeg. الإغلاق القسري/انقطاع الكهرباء قد يترك مؤقتًا؛ لا توجد ضمانة ضد أعطال القرص.
- جرّب صور JPEG/PNG في وضع النسخ قبل الاستبدال؛ الناتج WebP، وقد لا تقبله بعض برامجك.

CRF ليس نسبة فقد. رقم أقل يرفع الجودة والحجم عادةً. SSIM درجة تشابه بنيوي على العينة وليست حكمًا على جودة الفيديو كله أو اللون أو الصوت. لم نغيّر الدقة عمدًا، لكن الترميز نفسه قد يفقد تفاصيل. قياس العينة يستبعد وقت اختيار المرمّز وقياس SSIM من تقدير الترميز. الاستبدال يضيف الفحص الكامل.

## رفع الكود

راجع التغييرات في VS Code ثم:

```powershell
git add src tests installer .github Build-Release.ps1 README.md RELEASE-NOTES.md CHANGELOG.md docs
git commit -m "Release v0.1.1: clean replacement, clearer Arabic controls and sample quality estimates"
git push
```

في Actions افتح العملية الخضراء، وحمّل FramePress-Windows. فك الضغط وثبّت FramePress-v0.1.1-Setup.exe فوق النسخة السابقة، ثم اختبر البرنامج المثبت. لا يلزم حذف الإصدار القديم يدويًا عند استخدام نفس مجلد التثبيت.

## نشر الإصدار

Releases → Draft a new release → أنشئ tag باسم v0.1.1 على main، العنوان FramePress v0.1.1. استخدم RELEASE-NOTES.md للوصف وأرفق Setup.exe الجديد فقط. جرّبه قبل Publish release. الإصدار التجريبي يُنشر كـ Pre-release إذا ما زلت تجمع الملاحظات.

النسخ الاحتياطية القديمة للفيديو من 0.1.0 لا يحذفها هذا التحديث تلقائيًا. افحص الأصل والناتج قبل حذف أي نسخة قديمة يدويًا. لا تحذف مجلدات ImageRescue بناءً على الاسم؛ FramePress لا ينشئ هذه المجلدات.
