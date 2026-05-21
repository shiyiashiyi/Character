# 推送到 GitHub

本地仓库已在 `Character` 目录初始化并完成首次提交。

## 1. 在 GitHub 创建空仓库

1. 打开 https://github.com/new  
2. **Repository name**：例如 `Character` 或 `FrontStudy-Character`  
3. 选 **Public** 或 **Private**  
4. **不要**勾选 “Add a README”（本地已有）  
5. 创建仓库  

## 2. 关联远程并推送

在 PowerShell 中执行（把 `你的用户名` 和 `仓库名` 换成自己的）：

```powershell
cd <你的 Character 项目目录>

git remote add origin https://github.com/你的用户名/仓库名.git
git branch -M main
git push -u origin main
```

## 3. 克隆后本地配置（其他人 / 新电脑）

```powershell
# 后端连接字符串
copy api\FrontStudy.Api\appsettings.Development.json.example api\FrontStudy.Api\appsettings.Development.json
# 编辑其中的 Password

# 前端 API 地址
copy login\.env.example login\.env.development
```

## 已排除、不会上传的内容

见 `.gitignore`：`node_modules`、`bin/`、`obj/`、`.vs/`、`appsettings.Development.json`、`.env.development` 等。
