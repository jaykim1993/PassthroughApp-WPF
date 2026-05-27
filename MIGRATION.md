# PassthroughApp — Avalonia → WPF 마이그레이션 가이드

## 변경 사항 요약

### 삭제된 파일
| Avalonia | 이유 |
|----------|------|
| `App.axaml` | WPF `App.xaml`로 대체 |
| `App.axaml.cs` | WPF `App.xaml.cs`로 대체 |
| `Program.cs` | WPF는 별도 진입점 불필요 (자동 생성) |
| `ViewLocator.cs` | WPF는 DataTemplate으로 View 매핑 |
| `PassthroughApp.slnx` | 표준 `.sln`으로 대체 |

### 변경된 파일
| Avalonia | WPF | 주요 변경 |
|----------|-----|-----------|
| `*.axaml` | `*.xaml` | xmlns 네임스페이스 변경 |
| `App.axaml.cs` | `App.xaml.cs` | `OnFrameworkInitializationCompleted` → `OnStartup` |
| `PassthroughApp.csproj` | `PassthroughApp.csproj` | Avalonia 패키지 제거, `<UseWPF>true</UseWPF>` 추가 |

---

## 패키지 변경

### 제거 (Avalonia 전용)
```xml
<!-- 삭제 -->
<PackageReference Include="Avalonia" Version="12.0.1" />
<PackageReference Include="Avalonia.Desktop" Version="12.0.1" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="12.0.1" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="12.0.1" />
<PackageReference Include="AvaloniaUI.DiagnosticsSupport" Version="2.2.1" />
```

### 유지 (크로스 플랫폼 호환)
```xml
<!-- 그대로 사용 가능 -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.1" />
<PackageReference Include="SoundFlow" Version="1.4.1" />
```

---

## XAML 네임스페이스 변환

```xml
<!-- Avalonia -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

<!-- WPF -->
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
```

---

## ViewLocator 대체 방법

Avalonia의 `ViewLocator`(IDataTemplate)는 WPF에서 `DataTemplate`으로 대체합니다.

**방법 1: App.xaml에 직접 등록 (단순)**
```xml
<Application.Resources>
    <DataTemplate DataType="{x:Type vm:SomeViewModel}">
        <views:SomeView />
    </DataTemplate>
</Application.Resources>
```

**방법 2: DataTemplateSelector 사용 (동적)**
```csharp
public class ViewLocator : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is null) return base.SelectTemplate(item, container);
        var viewTypeName = item.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var viewType = Type.GetType(viewTypeName);
        if (viewType is null) return base.SelectTemplate(item, container);
        return new DataTemplate(item.GetType())
        {
            VisualTree = new FrameworkElementFactory(viewType)
        };
    }
}
```

---

## Avalonia 전용 컨트롤 → WPF 대응표

| Avalonia | WPF |
|----------|-----|
| `TextBlock` | `TextBlock` ✅ 동일 |
| `Button` | `Button` ✅ 동일 |
| `StackPanel` | `StackPanel` ✅ 동일 |
| `Grid` | `Grid` ✅ 동일 |
| `NumericUpDown` | `⚠️ 없음` → 직접 구현 또는 MahApps.Metro 사용 |
| `Expander` | `Expander` ✅ 동일 |
| `Slider` | `Slider` ✅ 동일 |
| `ComboBox` | `ComboBox` ✅ 동일 |
| `PathIcon` | `⚠️ 없음` → `Path` 또는 이미지 사용 |
| `TransitioningContentControl` | `⚠️ 없음` → 직접 구현 |

---

## TargetFramework 변경

```xml
<!-- Avalonia: 크로스 플랫폼 -->
<TargetFramework>net10.0</TargetFramework>

<!-- WPF: Windows 전용 -->
<TargetFramework>net10.0-windows</TargetFramework>
```

---

## 빌드 방법

```bash
dotnet restore
dotnet build
dotnet run
```

> **주의**: WPF는 Windows 전용입니다. Linux/macOS에서는 실행되지 않습니다.
