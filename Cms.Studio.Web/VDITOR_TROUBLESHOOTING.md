<!-- 
故障排除指南: vditor 显示不出来

在 PostEdit.cshtml 页面打开浏览器开发者工具 (F12)，按以下步骤诊断:

1. 打开 Console 标签页，查看是否有错误信息:
   - 找 "Content Security Policy" 或 "CSP" 相关错误
   - 找 "Vditor" 相关错误
   - 找 "404" 或网络错误

2. 打开 Network 标签页，检查:
   - https://cdn.jsdelivr.net/npm/vditor@latest (状态应为 200)
   - https://cdn.jsdelivr.net/npm/vditor@latest/dist/index.css (状态应为 200)
   - 是否有被 CSP 阻止的请求

3. 原因排查:

   问题A: "Content Security Policy" 错误
   └─ 解决: 修改 Program.cs 中的 CSP 策略
   └─ 位置: app.Use(async (context, next) =>

   问题B: "Vditor is not defined" 错误
   └─ 原因: vditor.js 加载失败或 CDN 不可用
   └─ 解决: 检查网络连接, 或等待 CDN 响应

   问题C: CSS 不加载
   └─ 原因: style-src CSP 限制
   └─ 解决: 检查 CSP 中的 style-src 指令

4. 快速测试:
   在 Console 中输入: typeof Vditor
   - 返回 "function" = 正常加载
   - 返回 "undefined" = 加载失败

5. 缓存问题:
   - Ctrl + Shift + Delete 清空浏览器缓存
   - 或 Ctrl + F5 硬刷新页面

6. 如果仍未解决:
   - 检查 appsettings.json 中的 URL 配置
   - 确认没有代理或防火墙阻止 jsDelivr CDN
   - 在其他浏览器测试
-->
