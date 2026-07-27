using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ColorPickerControl.Sample.ViewModels;

/// <summary>INotifyPropertyChanged を実装する ViewModel 基底クラス。</summary>
/// <remarks>
/// サンプルアプリ用の最小限の MVVM 実装（外部ライブラリ非依存）。<br/>
/// </remarks>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>プロパティ変更通知イベント。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>プロパティ変更を通知する。</summary>
    /// <param name="propertyName">変更されたプロパティ名。</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>値の設定と変更通知を行う。</summary>
    /// <typeparam name="T">プロパティの型。</typeparam>
    /// <param name="field">バッキングフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="propertyName">プロパティ名。</param>
    /// <returns>値が変更された場合は <see langword="true"/>。</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
