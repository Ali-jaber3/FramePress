# أول إصدار عام v0.1.0: الكود، GitHub والمُثبّت

## 1. مراجعة التعديل
- أسماء CPU/GPU تظهر في الشريط الجانبي. القراءة من Windows CIM ولا تعتمد على تخمين اسم الكرت.
- Auto يقارن مشفرات NVIDIA/Intel/AMD والمعالج بعينة من المصدر حتى ثانيتين/48 إطارًا عند أبعاده الأصلية، ثم يختار الأسرع الذي نجح. تُخزن النتائج للجلسة حسب codec وعمق اللون والأبعاد والجودة والسرعة.
- الاختبار الأول يضيف وقتًا. هو قياس لعينة قصيرة يشمل بدء المشفر، وليس ضمانًا لأفضل جودة أو أفضل أداء في كل المشاهد.
- «قبل» هو حجم المصدر؛ «بعد · فعلي» يظهر بعد اكتمال ملف كامل؛ العينة لا تُعرض كأنها الناتج الكامل.
- «التوفير» = (حجم الأصل - حجم الناتج) / حجم الأصل × 100. في وضع النسخ أو بوجود backup، فرق الحجم لا يعني تحرير المساحة الفعلية.
- إلغاء «إظهار الحجم بعد الضغط ونسبة التوفير» يخفي العمودين فقط. يبقى تقرير CSV متاحًا.

## 2. الاختبار
من جذر المشروع:
```powershell
dotnet run --project .\tests\FramePress.Tests\FramePress.Tests.csproj -c Release
dotnet run --project .\src\FramePress.App\FramePress.App.csproj -c Release
```
توقع 37 اختبارًا قياسيًا، أو 38 مع --batch. الاختبار المحلي للمطور لا يثبت تشغيل GPU الفعلي في أجهزة ويندوز المختلفة. راجع سجل المشفرات في جهازك.

## 3. رفع الكود أول مرة
أنشئ Repository فارغًا باسم FramePress على https://github.com/new . لا تضف README من الموقع إذا سترفع المشروع المحلي كاملًا لأول مرة.

في VS Code داخل المجلد الذي يحتوي src وFramePress.slnx:
```powershell
git init
git add .
git commit -m "Release v0.1.0: smart encoder selection and before-after sizes"
git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/FramePress.git
git push -u origin main
```
بدل YOUR_USERNAME باسم حسابك. لا تفترض أنه اسم مجلد ويندوز.

إذا المستودع متصل أصلًا، افحص `git remote -v` واستعمل فقط:
```powershell
git status
git add .
git commit -m "Add hardware benchmarks and before-after size display"
git push
```
`.gitignore` يستبعد vendor وbin وobj وartifacts ونسخ الاسترجاع وسكربتات التحديث المحلية. كود التطبيق الحقيقي داخل src وtests وملفات البناء والتوثيق.

## 4. إنشاء التطبيق الذي يثبته المستخدم
لا يحتاج المستخدم النهائي VS Code أو Python أو SDK. يُنشئ Build-Release.ps1 نسخة ذاتية الاحتواء ومعها FFmpeg ومُثبّت NSIS.

المسار الأسهل: بعد رفع الكود، افتح GitHub → Actions → Build Windows installer. يعمل البناء عند push إلى main، ويمكن اختيار Run workflow يدويًا. ينفذ الاختبارات والبناء والمُثبّت وفحص WPF الحقيقي على Windows.

بعد نجاح التشغيل، افتح Artifacts وحمل FramePress-Windows ثم فك الضغط. المثبّت:
`FramePress-v0.1.0-Setup.exe`.

يمكن أيضًا البناء محليًا بعد تثبيت NSIS 3 ووضع FFmpeg الحقيقي في vendor:
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Release.ps1
```
الناتج في artifacts. مجلد publish نسخة محمولة كاملة؛ لا تنقل EXE وحده من بين ملفاته.

## 5. إصدار GitHub العام
بعد اكتمال الاختبارات، ضع علامة الإصدار على نفس commit الذي بُني منه المثبّت:
```powershell
git tag -a v0.1.0 -m "FramePress v0.1.0"
git push origin v0.1.0
```
سيُبنى الوسم نفسه عبر Actions أيضًا. افتح Releases → Draft a new release:
- Tag: v0.1.0.
- Title: FramePress v0.1.0.
- Description: محتوى RELEASE-NOTES.md.
- أرفق ملف FramePress-v0.1.0-Setup.exe من بناء الوسم الناجح.
- اختر Publish release بعد تجربة التثبيت على ويندوز.

زر تحميل التطبيق للمستخدم هو ملف Setup.exe في Releases. أما src والمستودع فهما للمطورين وتعديل الكود. Artifacts هي ملفات بناء وليست إصدارًا منشورًا تلقائيًا.

## 6. قبل اعتبار المُثبّت جاهزًا
اختبره على Windows 11 دون .NET/FFmpeg مثبتين مسبقًا، ثم جرّب اختصار Start وسطح المكتب وضغط ملف وإلغاء التثبيت. راجع THIRD-PARTY.md ومعلومات الترخيص والمصدر لتوزيعة FFmpeg قبل نشر المثبّت للعموم. النسخة غير موقعة رقميًا ما لم تضف شهادة خاصة بك.
