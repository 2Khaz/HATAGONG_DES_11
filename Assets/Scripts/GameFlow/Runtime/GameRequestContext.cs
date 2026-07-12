using System;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class GameRequestContext:MonoBehaviour
    {
        [SerializeField]private RequestType currentRequestType=RequestType.Normal;
        public RequestType CurrentRequestType=>currentRequestType;
        public event Action<RequestType> RequestChanged;
        public void SetRequest(RequestType value){if(currentRequestType==value)return;currentRequestType=value;RequestChanged?.Invoke(value);}
    }
}
