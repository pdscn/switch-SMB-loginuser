# CLAUDE.md

## Git 提交规范

**不要在提交信息中添加任何署名尾注**，包括但不限于：

- `Co-Authored-By: Claude <noreply@anthropic.com>`
- `🤖 Generated with [Claude Code](https://claude.com/claude-code)`
- 任何形式的 `Generated with`、`Co-Authored-By` 署名行

提交信息只写实质内容：标题 + 必要的正文说明。正文用中文。

提交历史里已有过一次全量重写（`git filter-branch` + 强推）专门用来清除这类尾注，
不要再引入。

## 构建

```
build.bat          # 编译到 dist\SmbUserSwitch.exe
run-tests.bat      # 编译并运行回归测试
```

使用系统自带的 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，
**仅支持 C# 5 语法** —— 不能用字符串插值（`$"..."`）、`?.`、表达式体成员、
自动属性初始化器、`nameof`、`out var`、元组。

## 本仓库的两个易踩点

**1. `.bat` 文件必须是 CRLF + GBK 编码**

否则 cmd 会把 `^` 续行符解析错，中文输出也是乱码。用 Write 工具写完 `.bat` 后
需要转换：

```bash
python -c "
import io
d = io.open('build.bat','rb').read().replace(b'\r\n',b'\n').replace(b'\n',b'\r\n')
io.open('build.bat','wb').write(d.decode('utf-8').encode('gbk'))
"
```

**2. 在 Git Bash 里调 csc.exe 必须加 `MSYS2_ARG_CONV_EXCL='*'`**

否则 `/nologo` 这类参数会被当成路径转换（`C:/Program Files/Git/nologo`）。
参数用 `-` 前缀而非 `/` 前缀，路径用反斜杠。

## 测试

`tests/regression/` 下的测试用**本机回环 SMB**（`\\localhost\Videos`）作为靶子，
不需要外部服务器。不要把它们指向真实的 `\\192.168.11.13` —— 那会断开用户正在用的会话。

## 代码约定

- 界面全自绘，视觉常量集中在 `src/UI/Theme.cs`
- 注释用中文，解释「为什么」而非「是什么」
- Win32 API 的坑（`WNetCancelConnection2` 的名字不对称、`NetUseEnum` vs
  `WNetEnumResource`）在 `src/Core/SmbSessionManager.cs` 里有详细注释，改动前先读
