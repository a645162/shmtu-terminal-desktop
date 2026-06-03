---
layout: home

hero:
  name: 海大终端
  text: .NET 桌面版文档
  tagline: 上海海事大学校园消费账单管理工具 — Avalonia 跨平台版本
  actions:
    - theme: brand
      text: 新手入门
      link: /user/get-started
    - theme: alt
      text: 高级说明
      link: /advanced/overview

features:
  - title: 账单同步
    details: 增量同步拉取最新消费，全量同步补齐历史，支持手动验证码和远程 OCR 自动识别
  - title: 消费统计
    details: 今日/本月消费概览、分类占比、趋势图、商户排行，一眼看清钱花在哪
  - title: 数据安全
    details: 本地加密存储，快照备份一键恢复，支持 CSV/JSON/钱迹导出
  - title: 跨平台桌面
    details: 基于 Avalonia 12 与 .NET 8，单一代码库覆盖 Windows / Linux / macOS
---

## 这软件能干什么

海大终端 .NET 桌面版帮你把校园一卡通的消费记录同步到电脑上，方便查看和统计。

核心流程很简单：**登录 → 同步 → 看账单**。

基于 Avalonia 12 + .NET 8 构建，使用 MVVM + ReactiveUI，类库与 UI 解耦，可复用到控制台、服务端等其他宿主。所有数据存储在本地，不上传到任何第三方服务器。

## 第一次用？

只需 5 步：

1. 新建一个身份（填你的名字就行）
2. 添加账号（学号 + 密码）
3. 启动本地 OCR 服务（可选）
4. 点一次同步
5. 去首页和账单页看结果

[跟着步骤走](/user/get-started)

## 三类文档，按需阅读

### 普通用户版

面向日常使用者，只讲怎么用，不涉及技术细节。

- [第一次使用](/user/get-started) — 5 步上手
- [怎么同步账单](/user/sync-bills) — 增量、全量、单账号的区别
- [怎么看统计](/user/check-stats) — 首页和统计页分别适合看什么
- [怎么备份恢复](/user/backup-and-restore) — 快照、导出、恢复的操作时机
- [常见问题](/user/faq) — 没有账单、一直要验证码、统计不对等

### 详细说明

面向想深入了解每个功能的用户，包含完整的操作说明和参数解释。

- [快速开始](/guide/quick-start) — 从安装到首次同步的完整流程
- [界面总览](/guide/interface-overview) — 主界面各区域的功能说明
- [身份与账号管理](/guide/identity-and-account) — 身份和账号的字段与管理策略
- [账单查询与同步](/guide/bills-and-sync) — 同步模式、验证码选择、去重与重建
- [统计分析](/guide/statistics-and-analysis) — 各类统计项的说明与解读方法
- [设置与数据](/guide/settings-and-data) — 每类设置的含义与推荐值
- [FAQ](/guide/faq) — 详细的问题排查指南

### 高级版本

面向想理解程序机制和架构的开发者与维护者。

- [总览](/advanced/overview) — 高级文档的阅读指引
- [应用结构](/advanced/app-structure) — Avalonia 视图层、ViewModel、Services 的三层架构
- [数据流与状态](/advanced/data-flow) — ReactiveUI 状态管理、ServiceLocator、跨层通信
- [同步与验证码](/advanced/sync-and-captcha) — BillSyncService 链路、验证码解析器
- [配置与存储](/advanced/config-and-storage) — TomlConfigService、SqlSugar 数据库、加密与快照
- [构建与发布](/advanced/build-and-release) — 开发构建、文档发布、生产打包
