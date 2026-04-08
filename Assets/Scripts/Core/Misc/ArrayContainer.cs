using System;

//to get around jagged arrays not being serializable, you can make an array of these instead
[Serializable]
public struct ArrayContainer<T>
{
    public T[] array;
}