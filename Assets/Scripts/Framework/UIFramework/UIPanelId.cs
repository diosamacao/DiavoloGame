using System;
using UnityEngine;

namespace Framework.UIFramework
{
    [Serializable]
    public struct UIPanelId : IEquatable<UIPanelId>
    {
        [SerializeField] private string value;

        public string Value => value;
        public bool IsValid => !string.IsNullOrWhiteSpace(value);

        public UIPanelId(string value)
        {
            this.value = value;
        }

        //重载，同时实现 IEquatable<UIPanelId>
        //IEquatable<UIPanelId> 的强类型比较
        public bool Equals(UIPanelId other)
        {
            //这么写是为了区分大小写
            return string.Equals(
                value,
                other.value,
                StringComparison.Ordinal);
        }
        //重写，属于运行时多态
        //重写 object.Equals；obj 不是 UIPanelId 时返回 false
        public override bool Equals(object obj)
        {
            return obj is UIPanelId other && Equals(other);
        }

        //Dictionary 和 HashSet 不会一开始就逐个调用 Equals，而是先调用 GetHashCode()
        //如果 a.Equals(b) == true，那么 a.GetHashCode() 必须等于 b.GetHashCode()
        public override int GetHashCode()
        {
            //这个StringComparer也是为了区分大小写
            return value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        }

        //默认情况下，一个结构体打印出来可能只是类型名,因此重写ToString打印出value
        public override string ToString()
        {
            return value ?? string.Empty;
        }
        
    }
}
