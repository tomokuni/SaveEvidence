using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SnipCaptureUI.Sample;

/// <summary>INotifyPropertyChanged の最小実装（サンプル用）。</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>プロパティ変更を通知する。</summary>
    /// <param name="propertyName">変更されたプロパティ名。</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>値の変更を検知してフィールドを更新し、変更時のみ通知する。</summary>
    /// <typeparam name="T">プロパティの型。</typeparam>
    /// <param name="field">バッキングフィールド。</param>
    /// <param name="value">新しい値。</param>
    /// <param name="propertyName">変更されたプロパティ名。</param>
    /// <returns>値が変更された場合は true。</returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
