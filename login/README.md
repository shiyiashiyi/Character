# 登录页 · Vue 3

高交互登录演示，**Vue 3 + Vite**，视觉风格参考 Apple：白色、洁净、系统字体与蓝色主色。

## 快速开始

```bash
cd Character/login
npm install
npm run dev
```

浏览器打开终端提示的地址（默认 `http://localhost:5173`）。

## 构建

```bash
npm run build
npm run preview
```

## 后端 API

需先启动 `Character/api/FrontStudy.Api`（默认 `http://localhost:5050`）。详见 `Character/README.md`。

- **注册**：切换到「注册」标签，填写邮箱与密码
- **登录**：切换到「登录」标签，滑动确认后提交

## 演示账号（可选）

也可在页面注册新账号；若数据库中有旧演示数据，需通过注册接口写入正确密码哈希。

## 项目结构

```
login/
├── index.html
├── vite.config.js
├── package.json
└── src/
    ├── main.js
    ├── App.vue
    ├── components/
    │   ├── LoginPage.vue
    │   ├── StatusRail.vue
    │   ├── SlideGate.vue
    │   └── SuccessOverlay.vue
    ├── composables/
    │   ├── useStatus.js
    │   └── useAuthForm.js
    └── styles/
        └── global.css
```

## 交互特性

- 顶部状态轨 + 进度条即时反馈
- 邮箱实时校验与后缀 Chips
- 密码强度四档指示
- 滑动确认防误触登录
- 分阶段登录进度（感知性能优化）
- 成功层毛玻璃过渡动画
