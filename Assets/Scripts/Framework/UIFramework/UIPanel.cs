using UnityEngine;
namespace Framework.UIFramework
{
    public abstract class UIPanel : MonoBehaviour
    {
        public bool IsOpen {get; private set;}
        public bool IsInitialized {get; private set;}

        internal void Create()
        {
            if (IsInitialized)
                return;
            
            OnCreate();
            IsInitialized = true;
        }

        internal void Open()
        {
            if (!IsInitialized)
                Create();

            if (IsOpen)
            {
                Refresh();
                return;
            }
            
            IsOpen = true;
            gameObject.SetActive(true);
            
            OnOpen();
            Refresh();
        }

        internal void Refresh()
        {
            if (!IsInitialized || !IsOpen)
                return;
            
            OnRefresh();
        }

        internal void Close()
        {
            if (!IsOpen)
                return;
            
            OnClose();
            
            gameObject.SetActive(false);
            IsOpen = false;
        }

        internal void Release()
        {
            if (IsInitialized == false)
                return;
            
            if (IsOpen)
                Close();
            
            OnRelease();
            IsInitialized = false;
        }
        
        protected virtual void OnCreate(){}
        protected virtual void OnOpen(){}
        protected virtual void OnRefresh(){}
        protected virtual void OnClose(){}
        protected virtual void OnRelease(){}
    }
}
