using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SDK_Manager_GUI.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged, System.IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 标记是否已注册消息接收器，用于避免重复注册
        /// </summary>
        internal bool IsMessengerRegistered { get; set; }

        /// <summary>
        /// 清理消息注册等资源。由 NavigationService 在 ViewModel 被替换时调用。
        /// </summary>
        public virtual void Dispose()
        {
            WeakMessenger.UnregisterAll(this);
        }
    }
}
