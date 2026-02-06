using System;

//to get around jagged arrays not being serializable, you can make an array of these instead
[Serializable]
public class ArrayContainer<T>
{
    public T[] array;
}