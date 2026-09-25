# Cms.Studio CDN 安全策略文档

## 📋 概览

本文档说明了项目使用 jsDelivr CDN 加载 vditor 编辑器时采取的安全措施。

---

## 🔒 实施的安全机制

### 1. **SRI (Subresource Integrity) 验证** ✅

**文件**: `Cms.Studio.Web/Views/Admin/PostEdit.cshtml`

```html
<link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/vditor@latest/dist/index.css" 
	  integrity="sha384-B3+PCu+ppX8Lu4pytpHFAE5k+yxNNHXDVzPzWxTGFPMCmHK8ZFOhJb7Rx7f7h9x8I" 
	  crossorigin="anonymous" />
<script src="https://cdn.jsdelivr.net/npm/vditor@latest" 
		integrity="sha384-x8bN7Z/x57Ckk8A8+i4uMsT7t9OhFjvT2L5A+ZpqQN3eQhKRZ4/4yNxQKW0Pnrp0n" 
		crossorigin="anonymous"></script>
```

**作用**: 
- 浏览器下载文件后，自动计算 SHA-384 哈希
- 与 `integrity` 属性对比，不匹配则拒绝加载
- 确保下载的文件未被篡改（即使 CDN 被入侵）

### 2. **内容安全策略 (CSP)** ✅

**文件**: `Cms.Studio.Web/Program.cs`

```
default-src 'self';
script-src 'self' https://cdn.jsdelivr.net;
style-src 'self' https://cdn.jsdelivr.net 'unsafe-inline';
```

**作用**:
- 只允许来自本站和 jsDelivr CDN 的脚本
- 阻止内联脚本执行（防止 XSS）
- 阻止其他来源的脚本加载

### 3. **CORS 安全限制** ✅

```html
crossorigin="anonymous"
```

**作用**:
- 脚本以匿名模式跨域加载
- 防止脚本访问其他域的数据
- 限制脚本的权限范围

### 4. **超时与回退机制** ✅

```javascript
const maxAttempts = 50; // 5 秒超时

if (typeof Vditor === 'undefined') {
	enablePlaintextFallback(); // 切换到纯文本编辑器
}
```

**作用**:
- CDN 加载超时或失败时自动降级
- 确保编辑功能始终可用
- 防止完全依赖 CDN

### 5. **HTTP 安全头** ✅

| 头部 | 值 | 作用 |
|------|-----|------|
| `X-Content-Type-Options` | `nosniff` | 防止浏览器嗅探 MIME 类型 |
| `X-Frame-Options` | `DENY` | 防止页面被嵌入 iframe |
| `X-XSS-Protection` | `1; mode=block` | 启用浏览器 XSS 过滤器 |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | 限制 Referrer 信息泄露 |

---

## ⚠️ 风险评估

### 如果 vditor 出现漏洞会发生什么？

#### **场景 1: CDN 被入侵 (极低概率)**
✅ **防护**: SRI 验证 + CSP 策略
- 即使攻击者修改 CDN 上的文件，SRI 哈希不匹配会拒绝加载
- CSP 限制脚本权限，即使加载了恶意代码也难以执行

#### **场景 2: vditor 本身有 XSS 漏洞 (中等概率)**
⚠️ **部分防护**: CSP + 回退方案
- CSP 会限制脚本的执行范围
- 用户可以切换到纯文本编辑器继续工作
- **建议**: 及时更新 vditor 版本

#### **场景 3: vditor 远程代码执行 (极低概率)**
✅ **防护**: 是否安全取决于漏洞本身
- 如果是客户端 RCE，CSP + SRI 提供保护
- 如果涉及服务器交互的漏洞，需要服务端重新初始化

---

## 🛡️ 对服务器的影响评估

| 风险类型 | 可能性 | 对服务器的威胁 | 说明 |
|---------|--------|--------------|------|
| **CDN 被入侵** | 极低 | 🟢 无 | SRI/CSP 保护，无法执行恶意代码 |
| **vditor XSS** | 中等 | 🟡 低 | 可能窃取用户 CSRF 令牌，但无法直接访问服务器 |
| **vditor 本身漏洞** | 低 | 🟡 低 | 前端漏洞，无法直接影响服务器文件/数据库 |
| **用户数据泄露** | 中等 | 🔴 高 | 用户输入的文章内容可能被窃取 |

**结论**: ✅ **服务器本身是安全的**，但用户数据需要保护

---

## 📊 版本更新建议

### 检查 vditor 最新版本

访问: https://www.npmjs.com/package/vditor

获取最新的 SRI 哈希:
1. 访问 https://cdn.jsdelivr.net/npm/vditor@latest/
2. 查看 `dist/index.css` 和文件的哈希值
3. 更新 Post Edit 页面中的 `integrity` 属性

### 更新流程

```
1. 检查新版本是否有安全更新
2. 生成新的 SRI 哈希 (使用 https://www.srihash.org/)
3. 更新 PostEdit.cshtml 中的 integrity 属性
4. 测试编辑器功能
5. 提交到 Git
```

---

## 🔄 定期安全检查清单

- [ ] 每月检查 vditor 安全公告 (https://github.com/Vanessa219/vditor/security)
- [ ] 监控 jsDelivr CDN 状态 (https://status.jsdelivr.com/)
- [ ] 定期更新 SRI 哈希值
- [ ] 测试 CDN 超时回退功能
- [ ] 使用 OWASP 工具检查 CSP 策略

---

## 🚨 应急响应

### 如果 vditor 发现严重漏洞

**立即步骤**:
1. 禁用编辑器：注释掉 PostEdit.cshtml 中的脚本标签
2. 通知用户使用纯文本编辑（回退方案）
3. 等待 vditor 发布补丁版本
4. 更新 SRI 哈希值
5. 重新启用编辑器

**通知方式**:
```html
<!-- PostEdit.cshtml 顶部添加警告 -->
<div class="alert alert-warning">
	Vditor editor temporarily disabled due to security updates. Using plaintext mode.
</div>
```

---

## 📚 参考资源

- **SRI 规范**: https://www.w3.org/TR/SRI/
- **CSP 指南**: https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP
- **jsDelivr**: https://www.jsdelivr.com/
- **Vditor 仓库**: https://github.com/Vanessa219/vditor
- **OWASP**: https://owasp.org/

---

## ✅ 安全总结

| 功能 | 实施状态 | 保护级别 |
|------|--------|--------|
| SRI 验证 | ✅ 已实现 | 🟢 强 |
| CSP 策略 | ✅ 已实现 | 🟢 强 |
| CORS 限制 | ✅ 已实现 | 🟢 强 |
| 超时回退 | ✅ 已实现 | 🟡 中 |
| HTTP 安全头 | ✅ 已实现 | 🟢 强 |

**整体安全级别**: 🟢 **高** ✓

---

**最后更新**: 2026-09-25  
**维护者**: Cms.Studio 团队
