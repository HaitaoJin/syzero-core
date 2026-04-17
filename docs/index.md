---
layout: home

hero:
  name: "SyZero"
  text: ".NET 模块化微服务框架"
  tagline: "为分布式应用提供开箱即用的服务治理、数据访问、动态 API 与可观测能力。"
  image:
    src: /icon/logo.png
    alt: SyZero Logo
  actions:
    - theme: brand
      text: 快速开始
      link: /guide/getting-started
    - theme: alt
      text: 模块总览
      link: /modules/index
    - theme: alt
      text: GitHub
      link: https://github.com/HaitaoJin/syzero-core

features:
  - title: 模块化装配
    details: 通过按需引用 NuGet 包快速组合服务治理、消息、缓存、数据访问等能力。
  - title: 云原生能力
    details: 内置 Consul/Nacos、Redis/RabbitMQ、OpenTelemetry 等组件集成。
  - title: 开发效率优先
    details: 动态 Web API、动态 gRPC、统一异常和依赖注入约定，降低样板代码。
  - title: 可扩展架构
    details: 支持 DDD 分层、仓储与工作单元抽象，适配不同规模业务演进。
---

## 为什么是 SyZero

SyZero 面向希望在 .NET 生态中快速构建微服务系统的团队，提供简洁一致的工程化体验。

1. 核心能力集中在约定与基础设施，业务模块保持纯粹。
2. 组件拆分清晰，可按场景选择最小依赖集。
3. 文档、示例、模块包版本与仓库保持同源维护。

## 下一步

1. 阅读 [快速开始](/guide/getting-started) 完成第一个服务。
2. 按需进入 [模块总览](/modules/index) 选择扩展能力。
3. 查看 [发布说明](/release-notes) 了解版本变更。