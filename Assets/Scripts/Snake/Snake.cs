﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Snake : MonoBehaviour
{
    public event System.Action OnEat;
    public event System.Action OnDeathEvent;

    public GameObject bodyPrefab;
    public Transform startPos;
    public int startSnakeSize = 5;
    public float speed = 5;
    public Material mat;
    public const float size = .15f;
    public LayerMask snakeMask;
    public LayerMask foodMask;
    public int numToGrowByPerFood = 2;
    public float wiggleSpeed = 2;
    public float wiggleDst = .2f;
    public float growSpeed = 1;
    public bool wiggle;
    float wiggleAmountOld;
    float wiggleTime;
    public const float spacing = size;
    int numEaten;
    int maxLength = 100;
    int visIndex;
    List<Transform> growingParts;
    bool dead;
    static LinkedList<MoveData> headPoints;

    Vector2 initialDirection = Vector2.up;
    Vector2 iOld;
    public float smoothMoveTime = .1f;
    Vector2 velocity;
    Vector2 smoothVelocityRef;
    List<SnakeSegment> snake;
    ScreenAreas screen;
    float lastDirChangeTime;
    public AudioClip[] eatSfx;
    public AudioClip deathSfx;
    bool success;

    public GameObject missileTaskPlayer;
    Bounds screenLeft;
    Bounds screenRight;

    struct MoveData
    {
        public Vector2 pos;
        public bool teleport;

        public MoveData(Vector2 pos, bool teleport)
        {
            this.pos = pos;
            this.teleport = teleport;
        }
    }

    void Start()
    {
        headPoints = new LinkedList<MoveData>();

        maxLength = numToGrowByPerFood * 64 + startSnakeSize + 10;
        screen = FindObjectOfType<ScreenAreas>();
        CreateSnake(startSnakeSize);
        growingParts = new List<Transform>();
        iOld = initialDirection;

		screenLeft = screen.topScreens[0].bounds;
        screenRight = screen.topScreens[3].bounds;
    }

    // Update is called once per frame
    void Update()
    {
        if (success)
        {
            return;
        }
        if (!dead)
        {
            wiggleTime += Time.deltaTime;
            Vector2 dir = Vector2.zero;
            Vector2 axis = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                dir = Vector2.up;
            }
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
			{
                dir = Vector2.left;
			}
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
			{
                dir = Vector2.down;
			}
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
			{
                dir = Vector2.right;
			}
            if (dir == Vector2.zero)
			{
                if (axis.sqrMagnitude == 1)
                {
                    dir = axis;
                }
                else
                {
                    dir = iOld;
                }
			}

            if (dir == -iOld)
            {
                dir = iOld;
            }

            if (dir != iOld)
            {
                if (Time.time - lastDirChangeTime > .075f)
                {
                    lastDirChangeTime = Time.time;
                    iOld = dir;
                }
                else
                {
                    dir = iOld;
                }
            }


            float wiggleAmount = Mathf.Sin(wiggleTime * wiggleSpeed) * wiggleDst;
            float deltaWiggle = wiggleAmount - wiggleAmountOld;
            wiggleAmountOld = wiggleAmount;
            Vector2 wiggleDir = new Vector2(-dir.y, dir.x);

            Vector2 targetVelocity = dir * speed;
            velocity = Vector2.SmoothDamp(velocity, targetVelocity, ref smoothVelocityRef, smoothMoveTime, float.MaxValue, Time.deltaTime);
            float wiggleFac = (wiggle) ? 1 : 0;
            Vector2 displacement = velocity * Time.deltaTime + wiggleDir * deltaWiggle * wiggleFac;

            float moveDst = displacement.magnitude;

            snake[0].Move(displacement, false);

			float headX = snake[0].position.x;
			bool left = headX + size / 2f > screenLeft.min.x && headX - size / 2f < screenLeft.max.x;
			bool right = headX + size / 2f > screenRight.min.x && headX - size / 2f < screenRight.max.x;
            float buffer = .1f;
            // left
            if (snake[0].position.x + size / 2f < screen.minMaxX.x)
            {
                Vector2 newPos = new Vector2(screen.minMaxX.y + size / 2f - buffer, snake[0].position.y);
                //print("left: " + newPos);
                snake[0].Move(newPos - snake[0].position,true);
            }
            //right
            if (snake[0].position.x - size / 2f > screen.minMaxX.y)
            {
                //print("right");
                Vector2 newPos = new Vector2(screen.minMaxX.x - size / 2f + buffer, snake[0].position.y);
                snake[0].Move(newPos - snake[0].position,true);
            }
            //down
            if (snake[0].position.y + size / 2f < screen.minMaxY.x)
            {
                
                Vector2 newPos = new Vector2(snake[0].position.x, screen.minMaxY.y + size / 2f - buffer);
                if (left)
                {
                    newPos = new Vector2(snake[0].position.x, screenLeft.max.y + size / 2f - buffer);
                }
				if (right)
				{
                    newPos = new Vector2(snake[0].position.x, screenRight.max.y + size / 2f - buffer);
				}
                snake[0].Move(newPos - snake[0].position,true);
            }
            //up

            float upPos = snake[0].position.y - size / 2f;
            if (upPos > screenRight.max.y)
            {
                
                if (left && upPos > screenLeft.max.y || right || upPos > screen.minMaxY.y)
                {
                    Vector2 newPos = new Vector2(snake[0].position.x, screen.minMaxY.x - size / 2f + buffer);
                    snake[0].Move(newPos - snake[0].position, true);
                }
            }

            for (int i = 1; i < snake.Count; i++)
            {
                snake[i].Follow(moveDst);
            }

            if (Physics2D.OverlapCircle(snake[0].position, size * .5f, snakeMask))
            {
                OnDeath();
            }

            Collider2D food = Physics2D.OverlapCircle(snake[0].position, size * .5f, foodMask);
            if (food != null)
            {
                Eat(food.gameObject);
            }

            if (growingParts.Count > 0)
            {
                float s = growingParts[0].localScale.x;
                s = Mathf.Clamp(s + Time.deltaTime * growSpeed * size, 0, size);
                growingParts[0].localScale = Vector3.one * s;
                if (s == size)
                {
                    growingParts[0].GetComponent<CircleCollider2D>().enabled = true;
                    growingParts.RemoveAt(0);
                }
            }

            /*
            for (int i = growingParts.Count-1; i >= 0; i--)
            {
                float s = growingParts[i].localScale.x;
                s = Mathf.Clamp(s + Time.deltaTime * growSpeed*size,0,size);
                growingParts[i].localScale = Vector3.one * s;
                if (s == size)
                {
                    growingParts.RemoveAt(i);
                }
            }
            */
        }
        else
        {
            for (int i = 0; i < visIndex; i++)
            {
                int numRem = visIndex - i;
                float startS = snake[i].t.localScale.x;
                snake[i].t.localScale = Vector3.MoveTowards(snake[i].t.localScale, Vector3.zero, (.5f+numRem * .2f) * Time.deltaTime);
                if (startS > 0)
                {
                    break;
                }
            }
        }
    }

    public void Success()
    {
        StartCoroutine(SuccessAnimation());
        success = true;
    }

	IEnumerator SuccessAnimation()
	{
        transform.parent = null;

        Instantiate(missileTaskPlayer, snake[0].position, Quaternion.identity);
        Destroy(snake[0].t.gameObject);
        float duration = 2;
        float p = 0;

        Vector3[] startSizes = new Vector3[snake.Count - 1];
        for (int i = 1; i < snake.Count; i++)
        {
            startSizes[i - 1] = snake[i].t.localScale;
        }

        while (p < 1)
        {
            p += Time.deltaTime / duration;
            for (int i = 1; i < snake.Count; i++)
            {
                snake[i].t.localScale = Vector3.Lerp(startSizes[i-1], Vector3.zero, p);
            }
            yield return null;
        }

        Destroy(gameObject);

    }

    void Eat(GameObject food) {
        if (!dead)
        {
            if (OnEat != null)
            {
                OnEat();
            }
            Sfx.Play(eatSfx[Random.Range(0, eatSfx.Length)], .75f);
            Destroy(food);
            //GrowSnake();
            for (int i = 0; i < numToGrowByPerFood; i++)
            {
                if (visIndex < maxLength)
                {
                    snake[visIndex].SetVisible(true);
                    growingParts.Add(snake[visIndex].t);
                    snake[visIndex].t.localScale = Vector3.zero;
                    snake[visIndex].t.GetComponent<CircleCollider2D>().enabled = false;
                }
                visIndex++;
            }

            numEaten++;
        }

    }

    void OnDeath()
    {
        if (!dead)
        {
            if (OnDeathEvent != null)
            {
                OnDeathEvent();
            }
            Sfx.Play(deathSfx, .2f);
            dead = true;
            for (int i = 0; i < visIndex; i++)
            {
                if (i < snake.Count)
                {
                    snake[i].t.GetComponent<MeshRenderer>().material.color = Color.red;
                }
            }
        }
    }

    void CreateSnake(int initialSize = 2)
    {   
        snake = new List<SnakeSegment>();
        snake.Add(CreateBodyPart(startPos.position, null, false));
        snake.Add(CreateBodyPart((Vector2)startPos.position - initialDirection * spacing, snake[0], false));

        for (int i = 0; i < maxLength - 2; i++)
        {
            bool last = i == maxLength - 3;
            GrowSnake(last);
            if (i+2 > initialSize)
            {
                snake[i + 2].SetVisible(false);
            }
        }
        visIndex = initialSize;
    }

    SnakeSegment CreateBodyPart(Vector2 position, SnakeSegment parent, bool isTail)
    {

        GameObject g = Instantiate(bodyPrefab);
     
        g.transform.localScale = Vector3.one * size;
        g.transform.parent = transform;
        g.transform.localEulerAngles = Vector3.right * 90;

        SnakeSegment p = new SnakeSegment(parent, position, g.transform,isTail);
        if (snake.Count > 1)
        {
            CircleCollider2D c = g.GetComponent<CircleCollider2D>();
            c.radius = .1f;
            c.isTrigger = true;
        }
        else
        {
            Destroy(g.GetComponent<CircleCollider2D>());
        }


        return p;
    }

    void GrowSnake(bool isTail)
    {

        SnakeSegment tail = snake[snake.Count - 1];
        Vector2 forwardDir = (snake[snake.Count - 2].position - tail.position).normalized;

        Vector2 position = tail.position - forwardDir * spacing;

        snake.Add(CreateBodyPart(position,tail,isTail));
    }


    public class SnakeSegment
    {
        public Vector2 position;
        public Vector2 target;
        public Transform t;
        LinkedListNode<MoveData> currNode;
        bool isTail;

        public SnakeSegment(SnakeSegment parentSegment, Vector2 position, Transform t, bool isTail)
        {
            this.t = t;
            this.isTail = isTail;
            t.position = position;
            this.position = position;

            if (parentSegment != null)
            {
                target = parentSegment.position;
                currNode = parentSegment.currNode;
            }
            else
            {
                currNode = headPoints.AddFirst(new MoveData(position, false));
            }
        }

        // This should only be used for the head of the snake
        public void Move(Vector2 moveAmount, bool teleport)
        {
            position += moveAmount;
            t.position = position;
            headPoints.AddLast(new MoveData(position, teleport));
        }


        public void Follow(float moveDst)
        {
            float moveDstRemaining = moveDst;

            while (moveDstRemaining > 0)
            {

                float dstToTarget = Vector2.Distance(position, target);

                if (dstToTarget <= moveDstRemaining)
                {
                    position = target;
                    moveDstRemaining -= dstToTarget;

                    currNode = currNode.Next;
                    MoveData md = currNode.Value;

                    target = md.pos;
                    if (md.teleport)
                    {
                        position = target;
                    }

                    if (isTail)
                    {
                        headPoints.RemoveFirst();

                    }

                }
                else
                {
                    Vector2 newPos = position + (target - position).normalized * moveDstRemaining;

                    position = newPos;
                    moveDstRemaining = 0;
                }

            }

            t.position = position;



        }

        public void SetVisible(bool v)
        {

            t.gameObject.SetActive(v);
        }


    }

}
